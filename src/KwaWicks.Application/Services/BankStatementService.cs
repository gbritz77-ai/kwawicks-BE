using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text;

namespace KwaWicks.Application.Services;

public class BankStatementService : IBankStatementService
{
    private readonly IBankStatementRepository _repo;
    private readonly IInvoiceService _invoiceService;
    private readonly IS3Service _s3;
    private const string CsvFolder = "bank-statements";

    public BankStatementService(
        IBankStatementRepository repo,
        IInvoiceService invoiceService,
        IS3Service s3)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
    }

    // ── Upload URL ─────────────────────────────────────────────────────────

    public async Task<(string UploadUrl, string S3Key)> GetUploadUrlAsync(string fileName, CancellationToken ct)
    {
        var safeFileName = Path.GetFileName(fileName);
        var key = $"{CsvFolder}/{Guid.NewGuid():N}_{safeFileName}";
        var url = await _s3.GeneratePresignedUploadUrlAsync(key, "text/csv", ct);
        return (url, key);
    }

    // ── Process uploaded CSV ───────────────────────────────────────────────

    public async Task<BankStatementResponse> ProcessUploadAsync(ProcessBankStatementRequest request, CancellationToken ct)
    {
        var csvContent = await _s3.DownloadTextAsync(request.S3Key, ct);
        var transactions = ParseCsv(csvContent);

        var credits = transactions.Where(t => t.Type == "Credit").ToList();

        var statement = new BankStatement
        {
            FileName         = request.FileName,
            S3Key            = request.S3Key,
            TransactionCount = transactions.Count,
            CreditCount      = credits.Count,
            TotalCredits     = credits.Sum(t => t.Amount),
            UploadedAt       = DateTime.UtcNow,
            Transactions     = transactions
        };

        await _repo.CreateAsync(statement, ct);
        return MapToResponse(statement);
    }

    // ── CRUD ───────────────────────────────────────────────────────────────

    public async Task<List<BankStatementSummaryResponse>> ListAsync(CancellationToken ct)
    {
        var list = await _repo.ListAsync(ct);
        return list.Select(MapToSummary).ToList();
    }

    public async Task<BankStatementResponse?> GetAsync(string statementId, CancellationToken ct)
    {
        var statement = await _repo.GetAsync(statementId, ct);
        return statement is null ? null : MapToResponse(statement);
    }

    // ── Allocation ─────────────────────────────────────────────────────────

    public async Task<BankStatementResponse> AllocateAsync(
        string statementId,
        string transactionId,
        AllocateBankTransactionRequest request,
        CancellationToken ct)
    {
        var statement = await _repo.GetAsync(statementId, ct)
            ?? throw new InvalidOperationException($"Bank statement {statementId} not found.");

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found in statement {statementId}.");

        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated to an invoice.");

        // Load the invoice to get its number
        var invoice = await _invoiceService.GetAsync(request.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {request.InvoiceId} not found.");

        // Mark the bank transaction as allocated
        tx.IsAllocated            = true;
        tx.AllocatedInvoiceId     = request.InvoiceId;
        tx.AllocatedInvoiceNumber = invoice.InvoiceNumber;
        tx.AllocatedAt            = DateTime.UtcNow;

        // Mark the invoice as reconciled via the invoice service
        await _invoiceService.ReconAsync(request.InvoiceId, new ReconRequest
        {
            ReferenceNumber = !string.IsNullOrWhiteSpace(tx.Reference) ? tx.Reference : tx.Description,
            Notes           = $"Bank statement: {statement.FileName}",
            ReceivedAt      = tx.Date
        }, ct);

        await _repo.UpdateAsync(statement, ct);
        return MapToResponse(statement);
    }

    public async Task<BankStatementResponse> DeallocateAsync(
        string statementId,
        string transactionId,
        CancellationToken ct)
    {
        var statement = await _repo.GetAsync(statementId, ct)
            ?? throw new InvalidOperationException($"Bank statement {statementId} not found.");

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found in statement {statementId}.");

        if (!tx.IsAllocated)
            throw new InvalidOperationException("Transaction is not currently allocated.");

        var allocatedInvoiceId = tx.AllocatedInvoiceId;

        // Clear the bank transaction allocation
        tx.IsAllocated            = false;
        tx.AllocatedInvoiceId     = "";
        tx.AllocatedInvoiceNumber = "";
        tx.AllocatedAt            = null;

        // Un-reconcile the invoice
        if (!string.IsNullOrWhiteSpace(allocatedInvoiceId))
            await _invoiceService.UnreconAsync(allocatedInvoiceId, ct);

        await _repo.UpdateAsync(statement, ct);
        return MapToResponse(statement);
    }

    // ── CSV Parser ─────────────────────────────────────────────────────────

    internal static List<BankTransaction> ParseCsv(string csvContent)
    {
        var lines = csvContent
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n');

        // Find the header row (first row with recognisable column names)
        int headerIdx = -1;
        string[]? headers = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Length < 2) continue;

            var lower = cols.Select(c => c.ToLowerInvariant().Trim()).ToArray();
            bool hasDate   = lower.Any(c => c.Contains("date"));
            bool hasAmount = lower.Any(c => c == "amount" || c.Contains("debit") || c.Contains("credit"));
            bool hasDesc   = lower.Any(c =>
                c.Contains("desc") || c.Contains("narrative") || c.Contains("particular") ||
                c.Contains("detail") || c.Contains("transaction") || c == "info" || c == "remarks");

            if (hasDate && (hasAmount || hasDesc))
            {
                headerIdx = i;
                headers   = lower;
                break;
            }
        }

        if (headerIdx < 0 || headers is null)
            throw new InvalidOperationException(
                "Could not detect CSV column headers. Expected columns: Date, Description/Narrative, Amount (or Debit/Credit).");

        // Map column indices
        int dateCol   = FindCol(headers, c => c.Contains("date") && !c.Contains("value") && !c.Contains("purchase"));
        if (dateCol < 0)
            dateCol   = FindCol(headers, c => c.Contains("date"));

        int descCol   = FindCol(headers, c =>
            c.Contains("desc") || c.Contains("narrative") || c.Contains("particular") || c.Contains("detail") || c == "info" || c == "remarks");
        if (descCol < 0)
            descCol   = FindCol(headers, c => c.Contains("transaction") && !c.Contains("date") && !c.Contains("type"));

        int amountCol = FindCol(headers, c => c == "amount" || c == "transaction amount");
        int debitCol  = FindCol(headers, c => c.Contains("debit") && !c.Contains("order") && !c.Contains("account"));
        int creditCol = FindCol(headers, c => c.Contains("credit") && !c.Contains("order") && !c.Contains("account"));
        int refCol    = FindCol(headers, c => c == "reference" || c == "ref" || c == "ref no" || c == "cheque no");
        if (refCol < 0)
            refCol    = FindCol(headers, c => c.Contains("ref") && !c.Contains("desc") && !c.Contains("narrative"));

        if (dateCol < 0)
            throw new InvalidOperationException("Could not find a Date column in the CSV headers.");

        var transactions = new List<BankTransaction>();

        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Length <= dateCol) continue;

            // Parse date — skip rows without a valid date (metadata rows)
            if (!TryParseDate(cols[dateCol], out var date)) continue;

            decimal amount = 0m;
            var     type   = "Debit";

            if (amountCol >= 0 && amountCol < cols.Length)
            {
                if (TryParseDecimal(cols[amountCol], out var amt))
                {
                    amount = Math.Abs(amt);
                    type   = amt >= 0m ? "Credit" : "Debit";
                }
            }
            else
            {
                // Separate Debit / Credit columns
                decimal debitAmt  = 0m, creditAmt = 0m;
                if (debitCol  >= 0 && debitCol  < cols.Length) TryParseDecimal(cols[debitCol],  out debitAmt);
                if (creditCol >= 0 && creditCol < cols.Length) TryParseDecimal(cols[creditCol], out creditAmt);

                if (creditAmt > 0m)      { amount = creditAmt; type = "Credit"; }
                else if (debitAmt > 0m)  { amount = debitAmt;  type = "Debit"; }
                else continue; // no amount on this row — skip
            }

            if (amount == 0m) continue;

            var description = descCol >= 0 && descCol < cols.Length ? cols[descCol].Trim() : "";
            var reference   = refCol  >= 0 && refCol  < cols.Length ? cols[refCol].Trim()  : "";

            transactions.Add(new BankTransaction
            {
                Date        = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                Description = description,
                Reference   = reference,
                Amount      = amount,
                Type        = type
            });
        }

        return transactions;
    }

    // ── CSV helpers ────────────────────────────────────────────────────────

    private static int FindCol(string[] headers, Func<string, bool> predicate)
        => Array.FindIndex(headers, h => predicate(h));

    private static string[] SplitCsvLine(string line)
    {
        var result  = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int ci = 0; ci < line.Length; ci++)
        {
            var ch = line[ci];
            if (ch == '"')
            {
                // Handle escaped quotes ("")
                if (inQuotes && ci + 1 < line.Length && line[ci + 1] == '"')
                {
                    current.Append('"');
                    ci++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yyyy",
        "yyyy/MM/dd", "yyyy-MM-dd",
        "dd-MM-yyyy", "d-MM-yyyy",
        "dd MMM yyyy", "d MMM yyyy",
        "dd MMMM yyyy", "d MMMM yyyy",
        "dd MMM yy", "d MMM yy",
        "MM/dd/yyyy",
        "yyyyMMdd",
    };

    private static bool TryParseDate(string raw, out DateTime date)
    {
        raw = raw.Trim().Trim('"').Trim('\'');
        if (string.IsNullOrWhiteSpace(raw)) { date = default; return false; }

        return DateTime.TryParseExact(raw, DateFormats,
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string raw, out decimal amount)
    {
        raw = raw.Trim().Trim('"').Trim('\'');
        if (string.IsNullOrWhiteSpace(raw)) { amount = 0; return false; }

        // Remove common formatting characters
        raw = raw.Replace(" ", "").Replace("\u00a0", ""); // regular and non-breaking spaces

        // Resolve ambiguous separators
        int lastDot   = raw.LastIndexOf('.');
        int lastComma = raw.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            if (lastDot > lastComma)
                raw = raw.Replace(",", "");          // "50,000.00" → "50000.00"
            else
                raw = raw.Replace(".", "").Replace(",", "."); // "50.000,00" → "50000.00"
        }
        else if (lastComma >= 0)
        {
            // Comma only — decimal if 1-2 digits follow, otherwise thousands
            var afterComma = raw.Substring(lastComma + 1);
            if (afterComma.Length <= 2)
                raw = raw.Replace(",", ".");
            else
                raw = raw.Replace(",", "");
        }

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static BankStatementResponse MapToResponse(BankStatement s) => new()
    {
        StatementId      = s.StatementId,
        FileName         = s.FileName,
        S3Key            = s.S3Key,
        TransactionCount = s.TransactionCount,
        CreditCount      = s.CreditCount,
        TotalCredits     = s.TotalCredits,
        UploadedAt       = s.UploadedAt.ToString("O", CultureInfo.InvariantCulture),
        AllocatedCount   = s.Transactions.Count(t => t.IsAllocated),
        Transactions     = s.Transactions.Select(MapTx).ToList()
    };

    private static BankStatementSummaryResponse MapToSummary(BankStatement s) => new()
    {
        StatementId      = s.StatementId,
        FileName         = s.FileName,
        TransactionCount = s.TransactionCount,
        CreditCount      = s.CreditCount,
        TotalCredits     = s.TotalCredits,
        UploadedAt       = s.UploadedAt.ToString("O", CultureInfo.InvariantCulture),
        AllocatedCount   = s.AllocatedCount
    };

    private static BankTransactionResponse MapTx(BankTransaction t) => new()
    {
        TransactionId          = t.TransactionId,
        Date                   = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Description            = t.Description,
        Reference              = t.Reference,
        Amount                 = t.Amount,
        Type                   = t.Type,
        IsAllocated            = t.IsAllocated,
        AllocatedInvoiceId     = t.AllocatedInvoiceId,
        AllocatedInvoiceNumber = t.AllocatedInvoiceNumber,
        AllocatedAt            = t.AllocatedAt.HasValue
            ? t.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
            : null
    };
}

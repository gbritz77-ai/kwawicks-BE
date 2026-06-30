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
    private readonly ISupplierService _supplierService;
    private readonly IClientService _clientService;
    private readonly IClientCreditService _clientCreditService;
    private readonly IS3Service _s3;
    private const string CsvFolder = "bank-statements";

    public BankStatementService(
        IBankStatementRepository repo,
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IClientService clientService,
        IClientCreditService clientCreditService,
        IS3Service s3)
    {
        _repo                = repo                ?? throw new ArgumentNullException(nameof(repo));
        _invoiceService      = invoiceService      ?? throw new ArgumentNullException(nameof(invoiceService));
        _supplierService     = supplierService     ?? throw new ArgumentNullException(nameof(supplierService));
        _clientService       = clientService       ?? throw new ArgumentNullException(nameof(clientService));
        _clientCreditService = clientCreditService ?? throw new ArgumentNullException(nameof(clientCreditService));
        _s3                  = s3                  ?? throw new ArgumentNullException(nameof(s3));
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

        // Flag any credit that matches (same date + amount) a transaction already allocated in a
        // different, previously-imported statement — almost always caused by overlapping date
        // ranges between two bank statement exports (e.g. re-downloading "last 30 days").
        await FlagCrossStatementDuplicatesAsync(transactions, ct);

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

    private async Task FlagCrossStatementDuplicatesAsync(List<BankTransaction> newTransactions, CancellationToken ct)
    {
        var newCredits = newTransactions.Where(t => t.Type == "Credit").ToList();
        if (newCredits.Count == 0) return;

        var allStatements = await _repo.ListAsync(ct);
        if (allStatements.Count == 0) return;

        foreach (var prior in allStatements)
        {
            // ListAsync returns summaries only — load the full statement to inspect transactions.
            var full = await _repo.GetAsync(prior.StatementId, ct);
            if (full is null) continue;

            foreach (var priorTx in full.Transactions.Where(t => t.IsAllocated))
            {
                foreach (var newTx in newCredits.Where(t => !t.IsPossibleDuplicate))
                {
                    if (newTx.Date.Date != priorTx.Date.Date) continue;
                    if (Math.Round(newTx.Amount, 2) != Math.Round(priorTx.Amount, 2)) continue;

                    newTx.IsPossibleDuplicate = true;
                    newTx.DuplicateOfStatementFileName = full.FileName;
                    newTx.DuplicateOfTransactionId = priorTx.TransactionId;
                    newTx.DuplicateOfAllocatedAt = priorTx.AllocatedAt;
                    newTx.DuplicateOfAllocationSummary = priorTx.AllocationType switch
                    {
                        "Invoice" => $"Invoice {(string.IsNullOrWhiteSpace(priorTx.AllocatedInvoiceNumber) ? priorTx.AllocatedInvoiceId : priorTx.AllocatedInvoiceNumber)}",
                        "Supplier" => $"Supplier {priorTx.AllocatedSupplierName}",
                        "ClientCredit" => $"Client Credit — {priorTx.AllocatedClientName}",
                        "NonClient" => $"Non-Client — {priorTx.NonClientDescription}",
                        _ => priorTx.AllocationType
                    };
                }
            }
        }
    }

    // ── CRUD ───────────────────────────────────────────────────────────────

    public async Task<List<BankStatementSummaryResponse>> ListAsync(CancellationToken ct)
    {
        var list = await _repo.ListAsync(ct);
        return list.Select(MapToSummary).ToList();
    }

    public async Task<BankStatementResponse?> GetAsync(
        string statementId,
        CancellationToken ct,
        string? search = null,
        decimal? amount = null)
    {
        var statement = await _repo.GetAsync(statementId, ct);
        return statement is null ? null : MapToResponse(statement, search, amount);
    }

    // ── Invoice allocation ─────────────────────────────────────────────────

    public async Task<AllocateResponse> AllocateAsync(
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
            throw new InvalidOperationException("Transaction is already allocated.");

        var invoice = await _invoiceService.GetAsync(request.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {request.InvoiceId} not found.");

        tx.IsAllocated            = true;
        tx.AllocationType         = "Invoice";
        tx.AllocatedInvoiceId     = request.InvoiceId;
        tx.AllocatedInvoiceNumber = invoice.InvoiceNumber;
        tx.AllocatedAt            = DateTime.UtcNow;

        await _invoiceService.ReconAsync(request.InvoiceId, new ReconRequest
        {
            ReferenceNumber = !string.IsNullOrWhiteSpace(tx.Reference) ? tx.Reference : tx.Description,
            Notes           = $"Bank statement: {statement.FileName}",
            ReceivedAt      = tx.Date,
            Amount          = tx.Amount
        }, ct);

        await _repo.UpdateAsync(statement, ct);

        // Reload invoice to get updated AmountPaid
        var updated = await _invoiceService.GetAsync(request.InvoiceId, ct);
        AllocationWarning? warning = updated is not null && updated.AmountPaid < updated.GrandTotal
            ? new AllocationWarning
            {
                Code             = "PARTIAL_PAYMENT",
                Message          = $"Partial payment of {tx.Amount:N2} applied. Outstanding balance: {updated.AmountOutstanding:N2}.",
                BankAmount       = tx.Amount,
                AllocationAmount = updated.AmountPaid,
                Difference       = updated.AmountOutstanding
            }
            : null;

        return new AllocateResponse
        {
            Statement = MapToResponse(statement),
            Warning   = warning
        };
    }

    // ── Non-client allocation ──────────────────────────────────────────────

    public async Task<AllocateResponse> AllocateNonClientAsync(
        string statementId,
        string transactionId,
        AllocateNonClientRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("A description is required for non-client allocations.");

        var statement = await _repo.GetAsync(statementId, ct)
            ?? throw new InvalidOperationException($"Bank statement {statementId} not found.");

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found in statement {statementId}.");

        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        tx.IsAllocated           = true;
        tx.AllocationType        = "NonClient";
        tx.NonClientDescription  = request.Description.Trim();
        tx.AllocatedAt           = DateTime.UtcNow;

        await _repo.UpdateAsync(statement, ct);

        AllocationWarning? warning = request.Amount > 0
            ? BuildAmountWarning(tx.Amount, request.Amount)
            : null;

        return new AllocateResponse
        {
            Statement = MapToResponse(statement),
            Warning   = warning
        };
    }

    // ── Supplier allocation ────────────────────────────────────────────────

    public async Task<AllocateResponse> AllocateSupplierAsync(
        string statementId,
        string transactionId,
        AllocateSupplierRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierId))
            throw new InvalidOperationException("A supplier is required.");

        var statement = await _repo.GetAsync(statementId, ct)
            ?? throw new InvalidOperationException($"Bank statement {statementId} not found.");

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found in statement {statementId}.");

        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        var supplier = await _supplierService.GetAsync(request.SupplierId, ct)
            ?? throw new InvalidOperationException($"Supplier {request.SupplierId} not found.");

        tx.IsAllocated            = true;
        tx.AllocationType         = "Supplier";
        tx.AllocatedSupplierId    = supplier.SupplierId;
        tx.AllocatedSupplierName  = supplier.Name;
        tx.NonClientDescription   = string.IsNullOrWhiteSpace(request.Notes) ? "" : request.Notes.Trim();
        tx.AllocatedAt            = DateTime.UtcNow;

        await _repo.UpdateAsync(statement, ct);

        return new AllocateResponse { Statement = MapToResponse(statement) };
    }

    // ── Client credit allocation ───────────────────────────────────────────

    public async Task<AllocateResponse> AllocateClientCreditAsync(
        string statementId,
        string transactionId,
        AllocateClientCreditRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            throw new InvalidOperationException("A client is required.");

        var statement = await _repo.GetAsync(statementId, ct)
            ?? throw new InvalidOperationException($"Bank statement {statementId} not found.");

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found in statement {statementId}.");

        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        var client = await _clientService.GetByIdAsync(request.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {request.ClientId} not found.");

        // Record the EFT deposit in the client credit ledger
        await _clientCreditService.AddDepositAsync(request.ClientId, new AddCreditDepositRequest
        {
            Amount        = tx.Amount,
            PaymentMethod = "EFT",
            Notes         = string.IsNullOrWhiteSpace(request.Notes)
                              ? $"Bank statement: {statement.FileName} — {tx.Description}"
                              : request.Notes.Trim(),
            CreatedByUserId = "BankRecon"
        }, ct);

        tx.IsAllocated          = true;
        tx.AllocationType       = "ClientCredit";
        tx.AllocatedClientId    = client.ClientId;
        tx.AllocatedClientName  = client.ClientName;
        tx.AllocatedAt          = DateTime.UtcNow;

        await _repo.UpdateAsync(statement, ct);

        return new AllocateResponse { Statement = MapToResponse(statement) };
    }

    // ── Deallocation ───────────────────────────────────────────────────────

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

        if (tx.AllocationType == "ClientCredit")
            throw new InvalidOperationException(
                "Client credit payments cannot be reversed here. " +
                "Open the Client Credit Ledger and add a manual adjustment to correct the balance.");

        var allocatedInvoiceId = tx.AllocatedInvoiceId;

        tx.IsAllocated            = false;
        tx.AllocationType         = "";
        tx.AllocatedInvoiceId     = "";
        tx.AllocatedInvoiceNumber = "";
        tx.NonClientDescription   = "";
        tx.AllocatedSupplierId    = "";
        tx.AllocatedSupplierName  = "";
        tx.AllocatedClientId      = "";
        tx.AllocatedClientName    = "";
        tx.AllocatedAt            = null;

        if (!string.IsNullOrWhiteSpace(allocatedInvoiceId))
            await _invoiceService.UnreconAsync(allocatedInvoiceId, tx.Amount, ct);

        await _repo.UpdateAsync(statement, ct);
        return MapToResponse(statement);
    }

    // ── Allocation report ──────────────────────────────────────────────────

    public async Task<List<BankReconAllocationReportItem>> GetAllocationReportAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        var statements = await _repo.ListAsync(ct);
        var result = new List<BankReconAllocationReportItem>();

        foreach (var s in statements)
        {
            foreach (var tx in s.Transactions.Where(t => t.IsAllocated))
            {
                if (from.HasValue && tx.Date < from.Value) continue;
                if (to.HasValue   && tx.Date > to.Value)   continue;

                result.Add(new BankReconAllocationReportItem
                {
                    StatementId            = s.StatementId,
                    FileName               = s.FileName,
                    TransactionId          = tx.TransactionId,
                    Date                   = tx.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Description            = tx.Description,
                    Reference              = tx.Reference,
                    Amount                 = tx.Amount,
                    Type                   = tx.Type,
                    AllocationType         = tx.AllocationType,
                    AllocatedInvoiceId     = tx.AllocatedInvoiceId,
                    AllocatedInvoiceNumber = tx.AllocatedInvoiceNumber,
                    NonClientDescription   = tx.NonClientDescription,
                    AllocatedSupplierId    = tx.AllocatedSupplierId,
                    AllocatedSupplierName  = tx.AllocatedSupplierName,
                    AllocatedClientId      = tx.AllocatedClientId,
                    AllocatedClientName    = tx.AllocatedClientName,
                    AllocatedAt            = tx.AllocatedAt.HasValue
                        ? tx.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
                        : null
                });
            }
        }

        return result.OrderByDescending(r => r.Date).ToList();
    }

    // ── Amount mismatch warning ────────────────────────────────────────────

    private static AllocationWarning? BuildAmountWarning(decimal bankAmount, decimal allocationAmount)
    {
        var diff = Math.Round(bankAmount, 2) - Math.Round(allocationAmount, 2);
        if (diff == 0m) return null;

        return new AllocationWarning
        {
            Code             = "AMOUNT_MISMATCH",
            Message          = $"Bank payment of {bankAmount:N2} does not match allocation amount of {allocationAmount:N2}. Difference: {diff:N2}.",
            BankAmount       = bankAmount,
            AllocationAmount = allocationAmount,
            Difference       = diff
        };
    }

    // ── CSV Parser ─────────────────────────────────────────────────────────

    internal static List<BankTransaction> ParseCsv(string csvContent)
    {
        var lines = csvContent
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n');

        // ── FNB / Standard Bank HIST format (no header row) ──────────────────
        // Rows look like:  HIST,20260528,,12418.34,CREDIT TRANSFER,YOCO …,6088,0
        //   col[0]=HIST  col[1]=date(yyyyMMdd)  col[2]=empty|##
        //   col[3]=signed-amount  col[4]=tx-type  col[5]=description
        if (lines.Any(l => l.StartsWith("HIST,", StringComparison.OrdinalIgnoreCase)))
            return ParseFnbHistFormat(lines);

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

            // Skip rows without a valid date (metadata rows)
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
                decimal debitAmt  = 0m, creditAmt = 0m;
                if (debitCol  >= 0 && debitCol  < cols.Length) TryParseDecimal(cols[debitCol],  out debitAmt);
                if (creditCol >= 0 && creditCol < cols.Length) TryParseDecimal(cols[creditCol], out creditAmt);

                if (creditAmt > 0m)      { amount = creditAmt; type = "Credit"; }
                else if (debitAmt > 0m)  { amount = debitAmt;  type = "Debit"; }
                else continue;
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

    // ── FNB / Standard Bank HIST-format parser ────────────────────────────
    // Format (no header row):
    //   HIST,YYYYMMDD,{empty|##},signedAmount,TxType,Description,code,0
    // Non-HIST rows are metadata (branch info, open/close balances) — skipped.

    private static List<BankTransaction> ParseFnbHistFormat(string[] lines)
    {
        var transactions = new List<BankTransaction>();

        foreach (var rawLine in lines)
        {
            if (!rawLine.StartsWith("HIST,", StringComparison.OrdinalIgnoreCase))
                continue;

            var cols = SplitCsvLine(rawLine);
            if (cols.Length < 6) continue;

            // col[1] = date (yyyyMMdd)
            if (!TryParseDate(cols[1], out var date)) continue;

            // col[3] = signed amount  (+ve = credit, −ve = debit)
            if (!TryParseDecimal(cols[3], out var signedAmount)) continue;
            if (signedAmount == 0m) continue;

            var txType      = cols[4].Trim();                              // e.g. "CREDIT TRANSFER"
            var narrative   = cols.Length > 5 ? cols[5].Trim() : "";      // e.g. "YOCO 5V14U 280526"

            // Build a readable description: "CREDIT TRANSFER — YOCO 5V14U 280526"
            var description = string.IsNullOrWhiteSpace(narrative)
                ? txType
                : $"{txType} — {narrative}";

            transactions.Add(new BankTransaction
            {
                Date        = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                Description = description,
                Reference   = narrative,
                Amount      = Math.Abs(signedAmount),
                Type        = signedAmount >= 0m ? "Credit" : "Debit",
            });
        }

        if (transactions.Count == 0)
            throw new InvalidOperationException(
                "No HIST transaction rows could be parsed from this bank statement.");

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

        raw = raw.Replace(" ", "").Replace(" ", "");

        int lastDot   = raw.LastIndexOf('.');
        int lastComma = raw.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            if (lastDot > lastComma)
                raw = raw.Replace(",", "");
            else
                raw = raw.Replace(".", "").Replace(",", ".");
        }
        else if (lastComma >= 0)
        {
            var afterComma = raw.Substring(lastComma + 1);
            if (afterComma.Length <= 2)
                raw = raw.Replace(",", ".");
            else
                raw = raw.Replace(",", "");
        }

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static BankStatementResponse MapToResponse(
        BankStatement s,
        string? search = null,
        decimal? amount = null)
    {
        // Possible duplicates are hidden from the main working list — they're surfaced
        // separately so staff don't accidentally re-allocate a payment already reconciled
        // under a different (overlapping) statement upload.
        var possibleDuplicates = s.Transactions
            .Where(t => !t.IsAllocated && t.IsPossibleDuplicate)
            .ToList();

        var unallocated = s.Transactions
            .Where(t => !t.IsAllocated && !t.IsPossibleDuplicate)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            unallocated = unallocated
                .Where(t =>
                    t.Description.ToLowerInvariant().Contains(term) ||
                    t.Reference.ToLowerInvariant().Contains(term))
                .ToList();
        }

        if (amount.HasValue)
            unallocated = unallocated.Where(t => t.Amount == amount.Value).ToList();

        return new BankStatementResponse
        {
            StatementId      = s.StatementId,
            FileName         = s.FileName,
            S3Key            = s.S3Key,
            TransactionCount = s.TransactionCount,
            CreditCount      = s.CreditCount,
            TotalCredits     = s.TotalCredits,
            AllocatedCount   = s.Transactions.Count(t => t.IsAllocated && t.Type == "Credit"),
            UnallocatedCount = s.Transactions.Count(t => !t.IsAllocated && !t.IsPossibleDuplicate && t.Type == "Credit"),
            UnallocatedAmount = s.Transactions
                .Where(t => !t.IsAllocated && !t.IsPossibleDuplicate && t.Type == "Credit")
                .Sum(t => t.Amount),
            UploadedAt       = s.UploadedAt.ToString("O", CultureInfo.InvariantCulture),
            Transactions     = unallocated.Select(MapTx).ToList(),
            PossibleDuplicates = possibleDuplicates.Select(MapTx).ToList()
        };
    }

    private static BankStatementSummaryResponse MapToSummary(BankStatement s) => new()
    {
        StatementId      = s.StatementId,
        FileName         = s.FileName,
        TransactionCount = s.TransactionCount,
        CreditCount      = s.CreditCount,
        TotalCredits     = s.TotalCredits,
        UploadedAt       = s.UploadedAt.ToString("O", CultureInfo.InvariantCulture),
        AllocatedCount   = s.Transactions.Count(t => t.IsAllocated && t.Type == "Credit"),
        UnallocatedCount = s.Transactions.Count(t => !t.IsAllocated && !t.IsPossibleDuplicate && t.Type == "Credit"),
        UnallocatedAmount = s.Transactions
            .Where(t => !t.IsAllocated && !t.IsPossibleDuplicate && t.Type == "Credit")
            .Sum(t => t.Amount)
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
        AllocationType         = t.AllocationType,
        AllocatedInvoiceId     = t.AllocatedInvoiceId,
        AllocatedInvoiceNumber = t.AllocatedInvoiceNumber,
        NonClientDescription   = t.NonClientDescription,
        AllocatedSupplierId    = t.AllocatedSupplierId,
        AllocatedSupplierName  = t.AllocatedSupplierName,
        AllocatedClientId      = t.AllocatedClientId,
        AllocatedClientName    = t.AllocatedClientName,
        IsPossibleDuplicate           = t.IsPossibleDuplicate,
        DuplicateOfStatementFileName  = t.DuplicateOfStatementFileName,
        DuplicateOfAllocationSummary  = t.DuplicateOfAllocationSummary,
        DuplicateOfAllocatedAt        = t.DuplicateOfAllocatedAt.HasValue
            ? t.DuplicateOfAllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
            : null,
        AllocatedAt            = t.AllocatedAt.HasValue
            ? t.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
            : null
    };
}

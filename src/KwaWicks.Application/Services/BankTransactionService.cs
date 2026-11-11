using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Application.Services;

public class BankTransactionService : IBankTransactionService
{
    private readonly IBankTransactionRepository _repo;
    private readonly IBankStatementRepository _stmtRepo;
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IClientService _clientService;
    private readonly IClientCreditService _clientCreditService;
    private readonly IS3Service _s3;
    private readonly IPettyCashRepository _pettyCashRepo;
    private const string CsvFolder = "bank-statements";

    public BankTransactionService(
        IBankTransactionRepository repo,
        IBankStatementRepository stmtRepo,
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IClientService clientService,
        IClientCreditService clientCreditService,
        IS3Service s3,
        IPettyCashRepository pettyCashRepo)
    {
        _repo                = repo;
        _stmtRepo            = stmtRepo;
        _invoiceService      = invoiceService;
        _supplierService     = supplierService;
        _clientService       = clientService;
        _clientCreditService = clientCreditService;
        _s3                  = s3;
        _pettyCashRepo       = pettyCashRepo;
    }

    // ── Upload URL ─────────────────────────────────────────────────────────

    public async Task<(string UploadUrl, string S3Key)> GetUploadUrlAsync(string fileName, CancellationToken ct)
    {
        var safeFileName = Path.GetFileName(fileName);
        var key = $"{CsvFolder}/{Guid.NewGuid():N}_{safeFileName}";
        var url = await _s3.GeneratePresignedUploadUrlAsync(key, "text/csv", ct);
        return (url, key);
    }

    // ── Import CSV ─────────────────────────────────────────────────────────

    public async Task<ImportTransactionResult> ImportAsync(string s3Key, string fileName, CancellationToken ct)
    {
        var csvContent = await _s3.DownloadTextAsync(s3Key, ct);
        var incoming = BankStatementService.ParseCsv(csvContent);

        var existingKeys = await _repo.GetDedupKeysAsync(ct);

        var newTxs = incoming
            .Where(t => !existingKeys.Contains(DedupKey(t)))
            .ToList();

        int duplicatesSkipped = incoming.Count - newTxs.Count;

        await _repo.BatchCreateAsync(newTxs, ct);

        var (importCount, _) = await _repo.GetImportConfigAsync(ct);
        importCount++;
        var now = DateTime.UtcNow;
        await _repo.SaveImportConfigAsync(importCount, now, ct);

        var allTx = await _repo.ListAsync(ct);
        int pendingCount  = allTx.Count(t => !t.IsAllocated);
        int reconnedCount = allTx.Count(t => t.IsAllocated);

        return new ImportTransactionResult
        {
            NewTransactionsAdded = newTxs.Count,
            DuplicatesSkipped    = duplicatesSkipped,
            TotalTransactions    = allTx.Count,
            PendingCount         = pendingCount,
            ReconnedCount        = reconnedCount,
            LastImportedAt       = now.ToString("O", CultureInfo.InvariantCulture),
            ImportCount          = importCount
        };
    }

    private static string DedupKey(BankTransaction t)
        => $"{t.Date.Date:yyyy-MM-dd}|{t.Type}|{Math.Round(t.Amount, 2):F2}|{t.Description.Trim().ToUpperInvariant()}";

    // ── List ───────────────────────────────────────────────────────────────

    public async Task<FlatTransactionListResponse> ListAsync(
        string? type, string? status, string? search,
        DateTime? from, DateTime? to,
        CancellationToken ct)
    {
        var allTx = await _repo.ListAsync(ct);
        var (importCount, lastImportedAt) = await _repo.GetImportConfigAsync(ct);

        IEnumerable<BankTransaction> q = allTx;

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
            q = q.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue) q = q.Where(t => t.Date.Date >= from.Value.Date);
        if (to.HasValue)   q = q.Where(t => t.Date.Date <= to.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(t =>
                t.Description.ToLowerInvariant().Contains(term) ||
                t.Reference.ToLowerInvariant().Contains(term) ||
                t.Amount.ToString(CultureInfo.InvariantCulture).Contains(term));
        }

        var filtered = q.ToList();

        IEnumerable<BankTransaction> pending;
        IEnumerable<BankTransaction> reconned;

        if (status == "pending")
        {
            pending  = filtered.Where(t => !t.IsAllocated);
            reconned = Enumerable.Empty<BankTransaction>();
        }
        else if (status == "reconned")
        {
            pending  = Enumerable.Empty<BankTransaction>();
            reconned = filtered.Where(t => t.IsAllocated);
        }
        else
        {
            pending  = filtered.Where(t => !t.IsAllocated);
            reconned = filtered.Where(t => t.IsAllocated);
        }

        var pendingList  = pending.OrderByDescending(t => t.Date).ToList();
        var reconnedList = reconned.OrderByDescending(t => t.AllocatedAt ?? t.Date).ToList();

        return new FlatTransactionListResponse
        {
            Pending       = pendingList.Select(MapTx).ToList(),
            Reconned      = reconnedList.Select(MapTx).ToList(),
            PendingCount  = pendingList.Count,
            ReconnedCount = reconnedList.Count,
            PendingAmount = pendingList.Where(t => t.Type == "Credit").Sum(t => t.Amount),
            LastImportedAt = lastImportedAt == DateTime.MinValue
                ? null
                : lastImportedAt.ToString("O", CultureInfo.InvariantCulture),
            ImportCount   = importCount
        };
    }

    // ── Invoice allocation ─────────────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocateAsync(
        string transactionId,
        AllocateBankTransactionRequest request,
        CancellationToken ct)
    {
        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
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
            Notes           = "Bank recon",
            ReceivedAt      = tx.Date,
            Amount          = tx.Amount
        }, ct);

        await _repo.UpdateAsync(tx, ct);

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

        return new AllocateTransactionResponse { Transaction = MapTx(tx), Warning = warning };
    }

    // ── Non-client allocation ──────────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocateNonClientAsync(
        string transactionId,
        AllocateNonClientRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("A description is required for non-client allocations.");

        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        tx.IsAllocated          = true;
        tx.AllocationType       = "NonClient";
        tx.NonClientDescription = request.Description.Trim();
        tx.AllocatedAt          = DateTime.UtcNow;

        await _repo.UpdateAsync(tx, ct);

        AllocationWarning? warning = request.Amount > 0
            ? BuildAmountWarning(tx.Amount, request.Amount)
            : null;

        return new AllocateTransactionResponse { Transaction = MapTx(tx), Warning = warning };
    }

    // ── Supplier allocation ────────────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocateSupplierAsync(
        string transactionId,
        AllocateSupplierRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierId))
            throw new InvalidOperationException("A supplier is required.");

        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        var supplier = await _supplierService.GetAsync(request.SupplierId, ct)
            ?? throw new InvalidOperationException($"Supplier {request.SupplierId} not found.");

        tx.IsAllocated           = true;
        tx.AllocationType        = "Supplier";
        tx.AllocatedSupplierId   = supplier.SupplierId;
        tx.AllocatedSupplierName = supplier.Name;
        tx.NonClientDescription  = string.IsNullOrWhiteSpace(request.Notes) ? "" : request.Notes.Trim();
        tx.AllocatedAt           = DateTime.UtcNow;

        await _repo.UpdateAsync(tx, ct);
        return new AllocateTransactionResponse { Transaction = MapTx(tx) };
    }

    // ── Client credit allocation ───────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocateClientCreditAsync(
        string transactionId,
        AllocateClientCreditRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            throw new InvalidOperationException("A client is required.");

        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        var client = await _clientService.GetByIdAsync(request.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {request.ClientId} not found.");

        DateTime? occurredAt = null;
        if (!string.IsNullOrWhiteSpace(request.StatementDate) &&
            DateTime.TryParseExact(request.StatementDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            occurredAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        await _clientCreditService.AddDepositAsync(request.ClientId, new AddCreditDepositRequest
        {
            Amount          = tx.Amount,
            PaymentMethod   = "EFT",
            Notes           = string.IsNullOrWhiteSpace(request.Notes)
                                ? $"Bank recon — {tx.Description}"
                                : request.Notes.Trim(),
            CreatedByUserId = "BankRecon",
            OccurredAt      = occurredAt,
        }, ct);

        tx.IsAllocated         = true;
        tx.AllocationType      = "ClientCredit";
        tx.AllocatedClientId   = client.ClientId;
        tx.AllocatedClientName = client.ClientName;
        tx.AllocatedAt         = DateTime.UtcNow;

        await _repo.UpdateAsync(tx, ct);
        return new AllocateTransactionResponse { Transaction = MapTx(tx) };
    }

    // ── Split client credit allocation ─────────────────────────────────────

    public async Task<AllocateTransactionResponse> SplitClientCreditAsync(
        string transactionId,
        SplitClientCreditRequest request,
        CancellationToken ct)
    {
        if (request.Lines == null || request.Lines.Count < 2)
            throw new InvalidOperationException("A split allocation requires at least 2 lines.");
        if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.ClientId)))
            throw new InvalidOperationException("Every split line must have a client.");
        if (request.Lines.Any(l => l.Amount <= 0))
            throw new InvalidOperationException("Every split line amount must be greater than zero.");

        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        DateTime? occurredAt = null;
        if (!string.IsNullOrWhiteSpace(request.StatementDate) &&
            DateTime.TryParseExact(request.StatementDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            occurredAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
        }

        var splitLines = new List<BankTransactionSplitLine>();
        foreach (var line in request.Lines)
        {
            var client = await _clientService.GetByIdAsync(line.ClientId, ct)
                ?? throw new InvalidOperationException($"Client {line.ClientId} not found.");
            await _clientCreditService.AddDepositAsync(line.ClientId, new AddCreditDepositRequest
            {
                Amount          = line.Amount,
                PaymentMethod   = "EFT",
                Notes           = string.IsNullOrWhiteSpace(line.Notes)
                                    ? $"Bank recon split — {tx.Description}"
                                    : line.Notes.Trim(),
                CreatedByUserId = "BankRecon",
                OccurredAt      = occurredAt,
            }, ct);
            splitLines.Add(new BankTransactionSplitLine
            {
                ClientId   = client.ClientId,
                ClientName = client.ClientName,
                Amount     = line.Amount,
                Notes      = line.Notes,
            });
        }

        tx.IsAllocated         = true;
        tx.AllocationType      = "SplitClientCredit";
        tx.AllocatedAt         = DateTime.UtcNow;
        tx.SplitLines          = splitLines;
        tx.AllocatedClientName = string.Join(", ", splitLines.Select(l => l.ClientName));

        await _repo.UpdateAsync(tx, ct);
        return new AllocateTransactionResponse { Transaction = MapTx(tx) };
    }

    // ── Expense allocation ─────────────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocateExpenseAsync(
        string transactionId,
        AllocateExpenseRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
            throw new InvalidOperationException("An expense category is required.");

        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        tx.IsAllocated     = true;
        tx.AllocationType  = "Expense";
        tx.ExpenseCategory = request.Category.Trim();
        tx.AllocatedAt     = DateTime.UtcNow;

        await _repo.UpdateAsync(tx, ct);
        return new AllocateTransactionResponse { Transaction = MapTx(tx) };
    }

    // ── Petty cash allocation ──────────────────────────────────────────────

    public async Task<AllocateTransactionResponse> AllocatePettyCashAsync(string transactionId, CancellationToken ct)
    {
        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (tx.IsAllocated)
            throw new InvalidOperationException("Transaction is already allocated.");

        tx.IsAllocated    = true;
        tx.AllocationType = "PettyCash";
        tx.AllocatedAt    = DateTime.UtcNow;

        await _repo.UpdateAsync(tx, ct);
        return new AllocateTransactionResponse { Transaction = MapTx(tx) };
    }

    // ── Deallocation ───────────────────────────────────────────────────────

    public async Task<BankTransactionResponse> DeallocateAsync(string transactionId, CancellationToken ct)
    {
        var tx = await _repo.GetAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");
        if (!tx.IsAllocated)
            throw new InvalidOperationException("Transaction is not currently allocated.");
        if (tx.AllocationType == "ClientCredit" || tx.AllocationType == "SplitClientCredit")
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
        tx.ExpenseCategory        = "";
        tx.AllocatedAt            = null;
        tx.SplitLines             = new();

        if (!string.IsNullOrWhiteSpace(allocatedInvoiceId))
            await _invoiceService.UnreconAsync(allocatedInvoiceId, tx.Amount, ct);

        await _repo.UpdateAsync(tx, ct);
        return MapTx(tx);
    }

    // ── Expense categories ─────────────────────────────────────────────────

    public Task<List<string>> GetExpenseCategoriesAsync(CancellationToken ct)
        => _stmtRepo.GetExpenseCategoriesAsync(ct);

    public async Task<List<string>> AddExpenseCategoryAsync(string category, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new InvalidOperationException("Category name is required.");

        var categories = await _stmtRepo.GetExpenseCategoriesAsync(ct);
        var trimmed = category.Trim();
        if (!categories.Any(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            categories.Add(trimmed);
            categories.Sort(StringComparer.OrdinalIgnoreCase);
            await _stmtRepo.SaveExpenseCategoriesAsync(categories, ct);
        }
        return categories;
    }

    // ── Migration from old bank statements ─────────────────────────────────

    public async Task<MigrateResult> MigrateFromStatementsAsync(CancellationToken ct)
    {
        var existingKeys = await _repo.GetDedupKeysAsync(ct);
        var summaries = await _stmtRepo.ListAsync(ct);

        int migrated = 0;
        int skipped  = 0;

        var toCreate = new List<BankTransaction>();

        foreach (var summary in summaries)
        {
            var stmt = await _stmtRepo.GetAsync(summary.StatementId, ct);
            if (stmt is null) continue;

            foreach (var tx in stmt.Transactions)
            {
                var key = DedupKey(tx);
                if (existingKeys.Contains(key)) { skipped++; continue; }

                // Assign a fresh ID to avoid PK collision; preserve allocation state
                tx.TransactionId = Guid.NewGuid().ToString();
                toCreate.Add(tx);
                existingKeys.Add(key);
                migrated++;
            }
        }

        await _repo.BatchCreateAsync(toCreate, ct);

        // Update import config if it has never been set
        var (importCount, lastImported) = await _repo.GetImportConfigAsync(ct);
        if (importCount == 0 && summaries.Count > 0)
        {
            var latestImport = summaries.Max(s => s.LastImportedAt);
            await _repo.SaveImportConfigAsync(summaries.Count, latestImport, ct);
        }

        return new MigrateResult { Migrated = migrated, Skipped = skipped };
    }

    // ── Allocation report ──────────────────────────────────────────────────

    public async Task<List<BankReconAllocationReportItem>> GetAllocationReportAsync(
        DateTime? from, DateTime? to, CancellationToken ct)
    {
        var allTx = await _repo.ListAsync(ct);

        return allTx
            .Where(tx => tx.IsAllocated)
            .Where(tx => !from.HasValue || tx.Date >= from.Value)
            .Where(tx => !to.HasValue   || tx.Date <= to.Value)
            .OrderByDescending(tx => tx.Date)
            .Select(tx => new BankReconAllocationReportItem
            {
                StatementId            = "",
                FileName               = "",
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
                ExpenseCategory        = tx.ExpenseCategory,
                AllocatedAt            = tx.AllocatedAt.HasValue
                    ? tx.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
                    : null
            })
            .ToList();
    }

    // ── Debit report ───────────────────────────────────────────────────────

    public async Task<DebitReportResponse> GetDebitReportAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var allTx = await _repo.ListAsync(ct);

        var items = allTx
            .Where(t => t.Type == "Debit")
            .Where(t => !from.HasValue || t.Date.Date >= from.Value.Date)
            .Where(t => !to.HasValue   || t.Date.Date <= to.Value.Date)
            .Select(tx =>
            {
                var allocTo = tx.AllocationType switch
                {
                    "Expense"   => tx.ExpenseCategory,
                    "Supplier"  => tx.AllocatedSupplierName,
                    "NonClient" => tx.NonClientDescription,
                    "PettyCash" => "Petty Cash",
                    _           => ""
                };
                return new DebitReportItem
                {
                    StatementId    = "",
                    FileName       = "",
                    TransactionId  = tx.TransactionId,
                    Date           = tx.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Description    = tx.Description,
                    Reference      = tx.Reference,
                    Amount         = tx.Amount,
                    IsAllocated    = tx.IsAllocated,
                    AllocationType = tx.AllocationType,
                    AllocatedTo    = allocTo,
                    AllocatedAt    = tx.AllocatedAt.HasValue
                        ? tx.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
                        : null,
                    Source         = "Bank"
                };
            })
            .ToList();

        // Add petty cash Out entries
        var fromStr = from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toStr   = to?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var pcEntries = await _pettyCashRepo.ListEntriesAsync(fromStr, toStr, ct);
        foreach (var pc in pcEntries.Where(e => e.Type == "Out"))
        {
            items.Add(new DebitReportItem
            {
                StatementId    = "petty-cash",
                FileName       = "Petty Cash",
                TransactionId  = pc.EntryId,
                Date           = pc.EntryDate,
                Description    = pc.Description,
                Reference      = pc.RecipientName,
                Amount         = pc.Amount,
                IsAllocated    = true,
                AllocationType = "Expense",
                AllocatedTo    = pc.Category,
                AllocatedAt    = pc.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                Source         = "PettyCash",
            });
        }

        items = items
            .OrderByDescending(i => i.Date).ThenByDescending(i => i.Amount)
            .ToList();

        var totalDebits      = items.Sum(i => i.Amount);
        var totalAllocated   = items.Where(i => i.IsAllocated).Sum(i => i.Amount);
        var totalUnallocated = items.Where(i => !i.IsAllocated).Sum(i => i.Amount);

        var categoryGroups = items
            .Where(i => i.IsAllocated)
            .GroupBy(i => i.Source == "PettyCash"
                ? $"Petty Cash: {i.AllocatedTo}"
                : i.AllocationType switch
                {
                    "Expense"   => $"Expense: {i.AllocatedTo}",
                    "Supplier"  => $"Supplier: {i.AllocatedTo}",
                    "NonClient" => $"Non-Client: {i.AllocatedTo}",
                    "PettyCash" => "Petty Cash",
                    _           => i.AllocationType
                })
            .Select(g => new DebitReportCategorySummary { Label = g.Key, Total = g.Sum(x => x.Amount) })
            .OrderByDescending(g => g.Total)
            .ToList();

        if (totalUnallocated > 0)
            categoryGroups.Add(new DebitReportCategorySummary { Label = "Unallocated", Total = totalUnallocated });

        return new DebitReportResponse
        {
            TotalDebits      = totalDebits,
            TotalAllocated   = totalAllocated,
            TotalUnallocated = totalUnallocated,
            ByCategory       = categoryGroups,
            Items            = items
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────

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
        ExpenseCategory        = t.ExpenseCategory,
        AllocatedAt            = t.AllocatedAt.HasValue
            ? t.AllocatedAt.Value.ToString("O", CultureInfo.InvariantCulture)
            : null,
        SplitLines = t.SplitLines.Select(l => new SplitAllocationLineResponse
        {
            ClientId   = l.ClientId,
            ClientName = l.ClientName,
            Amount     = l.Amount,
            Notes      = l.Notes,
        }).ToList(),
    };
}

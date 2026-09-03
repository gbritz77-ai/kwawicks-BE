namespace KwaWicks.Application.DTOs;

public class ProcessBankStatementRequest
{
    public string S3Key { get; set; } = "";
    public string FileName { get; set; } = "";
}

public class AllocateBankTransactionRequest
{
    public string InvoiceId { get; set; } = "";
}

public class AllocateNonClientRequest
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class AllocateSupplierRequest
{
    public string SupplierId { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class AllocateClientCreditRequest
{
    public string ClientId { get; set; } = "";
    public string Notes { get; set; } = "";
    /// <summary>The bank statement transaction date (yyyy-MM-dd). Used as the ledger entry date instead of today.</summary>
    public string? StatementDate { get; set; }
}

public class AllocateExpenseRequest
{
    public string Category { get; set; } = "";
}

public class SplitAllocationLine
{
    public string ClientId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>Split one bank transaction across multiple client credit accounts.</summary>
public class SplitClientCreditRequest
{
    public List<SplitAllocationLine> Lines { get; set; } = new();
    /// <summary>The bank statement transaction date (yyyy-MM-dd). Used as the ledger entry date.</summary>
    public string? StatementDate { get; set; }
}

public class AddExpenseCategoryRequest
{
    public string Category { get; set; } = "";
}

public class AllocationWarning
{
    public string Code { get; set; } = "";          // "AMOUNT_MISMATCH"
    public string Message { get; set; } = "";
    public decimal BankAmount { get; set; }
    public decimal AllocationAmount { get; set; }
    public decimal Difference { get; set; }
}

public class AllocateResponse
{
    public BankStatementResponse Statement { get; set; } = new();
    public AllocationWarning? Warning { get; set; }
}

public class BankTransactionResponse
{
    public string TransactionId { get; set; } = "";
    public string Date { get; set; } = "";        // ISO date string
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";        // Credit | Debit
    public bool IsAllocated { get; set; }
    public string AllocationType { get; set; } = "";          // "Invoice" | "NonClient" | "Supplier" | "ClientCredit"
    public string AllocatedInvoiceId { get; set; } = "";
    public string AllocatedInvoiceNumber { get; set; } = "";
    public string NonClientDescription { get; set; } = "";
    public string AllocatedSupplierId { get; set; } = "";
    public string AllocatedSupplierName { get; set; } = "";
    public string AllocatedClientId { get; set; } = "";
    public string AllocatedClientName { get; set; } = "";
    public string ExpenseCategory { get; set; } = "";
    public string? AllocatedAt { get; set; }

    // ── Split allocation ─────────────────────────────────────────────────
    public List<SplitAllocationLineResponse> SplitLines { get; set; } = new();

    // ── Cross-statement duplicate detection ──────────────────────────────
    public bool IsPossibleDuplicate { get; set; }
    public string DuplicateOfStatementFileName { get; set; } = "";
    public string DuplicateOfAllocationSummary { get; set; } = "";
    public string? DuplicateOfAllocatedAt { get; set; }
}

public class SplitAllocationLineResponse
{
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Notes { get; set; } = "";
}

public class BankStatementResponse
{
    public string StatementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string S3Key { get; set; } = "";
    public int TransactionCount { get; set; }
    public int CreditCount { get; set; }
    public decimal TotalCredits { get; set; }
    public int AllocatedCount { get; set; }
    public int UnallocatedCount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public string UploadedAt { get; set; } = "";
    public List<BankTransactionResponse> Transactions { get; set; } = new();
    /// <summary>Unallocated transactions that match (same date + amount) a transaction already
    /// allocated in a different statement — hidden from the main list, shown separately with a warning.</summary>
    public List<BankTransactionResponse> PossibleDuplicates { get; set; } = new();
}

public class BankStatementSummaryResponse
{
    public string StatementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public int TransactionCount { get; set; }
    public int CreditCount { get; set; }
    public decimal TotalCredits { get; set; }
    public string UploadedAt { get; set; } = "";
    public int AllocatedCount { get; set; }
    public int UnallocatedCount { get; set; }
    public decimal UnallocatedAmount { get; set; }
}

public class DebitReportItem
{
    public string StatementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsAllocated { get; set; }
    public string AllocationType { get; set; } = "";   // "Expense" | "Supplier" | "NonClient" | ""
    public string AllocatedTo { get; set; } = "";      // category / supplier name / description
    public string? AllocatedAt { get; set; }
}

public class DebitReportResponse
{
    public decimal TotalDebits { get; set; }
    public decimal TotalAllocated { get; set; }
    public decimal TotalUnallocated { get; set; }
    public List<DebitReportCategorySummary> ByCategory { get; set; } = new();
    public List<DebitReportItem> Items { get; set; } = new();
}

public class DebitReportCategorySummary
{
    public string Label { get; set; } = "";   // "Expense: Fuel", "Supplier: ABC", "Unallocated", etc.
    public decimal Total { get; set; }
}

public class BankReconAllocationReportItem
{
    public string StatementId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string AllocationType { get; set; } = "";          // "Invoice" | "NonClient" | "Supplier" | "ClientCredit"
    public string AllocatedInvoiceId { get; set; } = "";
    public string AllocatedInvoiceNumber { get; set; } = "";
    public string NonClientDescription { get; set; } = "";
    public string AllocatedSupplierId { get; set; } = "";
    public string AllocatedSupplierName { get; set; } = "";
    public string AllocatedClientId { get; set; } = "";
    public string AllocatedClientName { get; set; } = "";
    public string ExpenseCategory { get; set; } = "";
    public string? AllocatedAt { get; set; }
}

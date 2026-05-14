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
    public string? AllocatedAt { get; set; }
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
    public string? AllocatedAt { get; set; }
}

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

public class BankTransactionResponse
{
    public string TransactionId { get; set; } = "";
    public string Date { get; set; } = "";        // ISO date string
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";        // Credit | Debit
    public bool IsAllocated { get; set; }
    public string AllocatedInvoiceId { get; set; } = "";
    public string AllocatedInvoiceNumber { get; set; } = "";
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
}

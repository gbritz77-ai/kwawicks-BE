namespace KwaWicks.Domain.Entities;

public class BankStatement
{
    public string StatementId { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = "";
    public string S3Key { get; set; } = "";
    public int TransactionCount { get; set; }
    public int CreditCount { get; set; }
    public decimal TotalCredits { get; set; }
    public int AllocatedCount { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public List<BankTransaction> Transactions { get; set; } = new();
}

public class BankTransaction
{
    public string TransactionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }        // always positive
    public string Type { get; set; } = "Credit"; // Credit | Debit
    public bool IsAllocated { get; set; }
    public string AllocatedInvoiceId { get; set; } = "";
    public string AllocatedInvoiceNumber { get; set; } = "";
    public DateTime? AllocatedAt { get; set; }
}

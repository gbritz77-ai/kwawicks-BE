namespace KwaWicks.Domain.Entities;

public class ClientCreditEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
    public string ClientId { get; set; } = "";

    /// <summary>Positive = money credited to account. Negative = charge/deduction.</summary>
    public decimal Amount { get; set; }

    /// <summary>Deposit | InvoiceCharge | ManualAdjustment</summary>
    public string EntryType { get; set; } = "";

    /// <summary>Cash | EFT | CardMachine — populated for deposits, empty for charges.</summary>
    public string PaymentMethod { get; set; } = "";

    /// <summary>Invoice ID for charges, or freeform note for deposits.</summary>
    public string Reference { get; set; } = "";

    public string Notes { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

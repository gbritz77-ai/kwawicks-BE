namespace KwaWicks.Domain.Entities;

public class Invoice
{
    public string InvoiceId { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string DeliveryOrderId { get; set; } = "";
    public string CreatedByDriverId { get; set; } = "";

    public string Status { get; set; } = "Confirmed"; // Draft, Confirmed, Cancelled, Paid

    public string PaymentType { get; set; } = ""; // Cash, EFT, Credit
    public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid
    public string ReceiptS3Key { get; set; } = "";

    public decimal SubTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class InvoiceLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
}

namespace KwaWicks.Domain.Entities;

public class Invoice
{
    public string InvoiceId { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceNumber { get; set; } = ""; // e.g. INV000001
    public string CustomerId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string DeliveryOrderId { get; set; } = "";
    public string CreatedByDriverId { get; set; } = "";

    public string SaleType { get; set; } = "Delivery"; // Delivery | HubDirect
    public string StaffMemberId { get; set; } = ""; // set when SaleType=HubDirect and buyer is staff

    public string Status { get; set; } = "Confirmed"; // Draft, Confirmed, Cancelled, Paid

    public string PaymentType { get; set; } = ""; // Cash, EFT, Credit
    public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid
    public string ReceiptS3Key { get; set; } = "";

    public decimal SubTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();
    public List<SplitPayment> SplitPayments { get; set; } = new();

    /// <summary>None | Pending | Approved | Amended</summary>
    public string PriceApprovalStatus { get; set; } = "None";
    public List<BelowCostLine> BelowCostLines { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SplitPayment
{
    public string Method { get; set; } = ""; // Cash | Card | EFT
    public decimal Amount { get; set; }
}

public class BelowCostLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
}

public class InvoiceLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
}

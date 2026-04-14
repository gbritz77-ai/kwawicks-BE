namespace KwaWicks.Application.DTOs;

public class BelowCostLineDto
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Shortfall => (CostPrice - SalePrice) * Quantity;
}

public class PriceApprovalResponse
{
    public string InvoiceId { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string SaleType { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public decimal GrandTotal { get; set; }
    public string PriceApprovalStatus { get; set; } = "Pending";
    public List<BelowCostLineDto> BelowCostLines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class AmendPriceRequest
{
    public List<AmendPriceLine> Lines { get; set; } = new();
}

public class AmendPriceLine
{
    public string SpeciesId { get; set; } = "";
    /// <summary>New ex-VAT unit price.</summary>
    public decimal NewUnitPrice { get; set; }
}

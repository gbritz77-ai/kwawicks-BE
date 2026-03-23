namespace KwaWicks.Domain.Entities;

public class ProcurementOrder
{
    public string ProcurementOrderId { get; set; } = Guid.NewGuid().ToString();
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string SupplierOrderReference { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string Notes { get; set; } = "";
    public string InvoiceS3Key { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public List<ProcurementOrderLine> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProcurementOrderLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal UnitCost { get; set; }
}

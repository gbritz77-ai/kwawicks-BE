namespace KwaWicks.Domain.Entities;

public class DriverStockAllocation
{
    public string AllocationId { get; set; } = Guid.NewGuid().ToString();
    public string DriverId { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Status { get; set; } = "Active"; // Active | Completed | Cancelled
    public string Notes { get; set; } = "";
    public List<DriverStockAllocationLine> Lines { get; set; } = new();
    public List<DriverSaleRecord> Sales { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class DriverStockAllocationLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int AllocatedQty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class DriverSaleRecord
{
    public string SaleId { get; set; } = Guid.NewGuid().ToString();
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentType { get; set; } = ""; // Cash | EFT
    public string CustomerName { get; set; } = "";
    public DateTime SoldAt { get; set; } = DateTime.UtcNow;
}

namespace KwaWicks.Application.DTOs;

public class CreateDriverStockAllocationRequest
{
    public string DriverId { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string? Notes { get; set; }
    public List<DriverStockAllocationLineRequest> Lines { get; set; } = new();
}

public class DriverStockAllocationLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class RecordDriverSaleRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string PaymentType { get; set; } = ""; // Cash | EFT
    public string? CustomerName { get; set; }
}

public class DriverStockAllocationResponse
{
    public string AllocationId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<DriverStockAllocationLineResponse> Lines { get; set; } = new();
    public List<DriverSaleRecordResponse> Sales { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DriverStockAllocationLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int AllocatedQty { get; set; }
    public int SoldQty { get; set; }
    public int RemainingQty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class DriverSaleRecordResponse
{
    public string SaleId { get; set; } = "";
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentType { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime SoldAt { get; set; }
}

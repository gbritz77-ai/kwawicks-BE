namespace KwaWicks.Domain.Entities;

public class DeliveryRun
{
    public string DeliveryRunId { get; set; } = Guid.NewGuid().ToString();
    public string HubId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string Status { get; set; } = "Open"; // Open | OutForDelivery | Completed
    public string Notes { get; set; } = "";
    public List<DeliveryRunAllocation> Allocations { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class DeliveryRunAllocation
{
    public string DeliveryOrderId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string DeliveryStatus { get; set; } = "Open"; // Open | OutForDelivery | Delivered
    public string InvoiceId { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public List<DeliveryRunAllocationLine> Lines { get; set; } = new();
}

public class DeliveryRunAllocationLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public int DeliveredQty { get; set; }
}

namespace KwaWicks.Domain.Entities;

public class CollectionRequest
{
    public string CollectionRequestId { get; set; } = Guid.NewGuid().ToString();
    public string ProcurementOrderId { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string Notes { get; set; } = "";
    public DateTime? CollectionDate { get; set; }
    public string InvoiceS3Key { get; set; } = "";
    public string DeliveryNoteS3Key { get; set; } = "";
    public List<CollectionRequestLine> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CollectionRequestLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public int LoadedQty { get; set; }
    public string LoadingNotes { get; set; } = "";
    public int ReceivedQty { get; set; }
    public string DiscrepancyNotes { get; set; } = "";
}

namespace KwaWicks.Application.DTOs;

public class CreateCollectionRequestRequest
{
    public string ProcurementOrderId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? CollectionDate { get; set; }
}

public class DriverLoadingUpdateRequest
{
    public List<CollectionLineLoadUpdate> Lines { get; set; } = new();
}

public class CollectionLineLoadUpdate
{
    public string SpeciesId { get; set; } = "";
    public int LoadedQty { get; set; }
    public string LoadingNotes { get; set; } = "";
}

public class HubConfirmReceiptRequest
{
    public List<CollectionLineReceiveUpdate> Lines { get; set; } = new();
}

public class CollectionLineReceiveUpdate
{
    public string SpeciesId { get; set; } = "";
    public int ReceivedQty { get; set; }
    public string DiscrepancyNotes { get; set; } = "";
}

public class FinanceAcknowledgeRequest
{
    public string InvoiceS3Key { get; set; } = "";
}

public class CollectionRequestResponse
{
    public string CollectionRequestId { get; set; } = "";
    public string ProcurementOrderId { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? CollectionDate { get; set; }
    public string InvoiceS3Key { get; set; } = "";
    public string DeliveryNoteS3Key { get; set; } = "";
    public List<CollectionRequestLineResponse> Lines { get; set; } = new();
    public List<CollectionDeliveryAllocationResponse> DeliveryAllocations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CollectionRequestLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public int LoadedQty { get; set; }
    public string LoadingNotes { get; set; } = "";
    public int ReceivedQty { get; set; }
    public string DiscrepancyNotes { get; set; } = "";
}

public class CollectionInvoiceUploadUrlResponse
{
    public string UploadUrl { get; set; } = "";
    public string S3Key { get; set; } = "";
}

public class AddDeliveryAllocationRequest
{
    public string ClientId { get; set; } = "";
    public List<AllocationLineRequest> Lines { get; set; } = new();
}

public class AllocationLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal? UnitPrice { get; set; }
}

public class CollectionDeliveryAllocationResponse
{
    public string DeliveryOrderId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public List<CollectionAllocationLineResponse> Lines { get; set; } = new();
}

public class CollectionAllocationLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

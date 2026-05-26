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
    public bool ShortfallFlagged { get; set; }
    public List<CollectionRequestLineResponse> Lines { get; set; } = new();
    public List<CollectionDeliveryAllocationResponse> DeliveryAllocations { get; set; } = new();
    public List<RoadsaleLineResponse> RoadsideSales { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ── Shortfall report ───────────────────────────────────────────────────────
public class CollectionShortfallReportItem
{
    public string CollectionRequestId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public DateTime? CollectionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public List<CollectionShortfallLine> ShortfallLines { get; set; } = new();
}

public class CollectionShortfallLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public int LoadedQty { get; set; }
    public int ShortfallQty { get; set; }
    public string LoadingNotes { get; set; } = "";
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

public class EditAllocationRequest
{
    public List<EditAllocationLine> Lines { get; set; } = new();
}

public class EditAllocationLine
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CollectionDeliveryAllocationResponse
{
    public string DeliveryOrderId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    /// <summary>Status of the linked delivery order (Open / AwaitingCollection / OutForDelivery / Delivered / MarkedAtHub / HubDirect).</summary>
    public string DeliveryStatus { get; set; } = "";
    /// <summary>Payment type from the linked invoice once the driver has invoiced (Cash / EFT / Credit / "").</summary>
    public string PaymentType { get; set; } = "";
    /// <summary>For HUB allocations: "" or "Accepted" once hub staff physically verified the stock.</summary>
    public string HubAcceptanceStatus { get; set; } = "";
    public List<CollectionAllocationLineResponse> Lines { get; set; } = new();
}

public class CollectionAllocationLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    /// <summary>Quantity allocated at planning time.</summary>
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>Actual quantity delivered to the client (0 = not yet delivered / invoice not yet created).</summary>
    public int DeliveredQty { get; set; }
    /// <summary>For HUB allocations: qty hub staff physically accepted into inventory (0 = not yet accepted).</summary>
    public int AcceptedQty { get; set; }
}

// ── Admin: confirm actual delivered qty + payment type ─────────────────────────
public class AdminConfirmDeliveryRequest
{
    public List<AdminConfirmDeliveryLine> Lines { get; set; } = new();
    /// <summary>Cash, EFT, Credit — recorded on the generated invoice.</summary>
    public string PaymentType { get; set; } = "";
}

public class AdminConfirmDeliveryLine
{
    public string SpeciesId { get; set; } = "";
    /// <summary>How many the client actually took. Remainder is treated as NotWanted return.</summary>
    public int DeliveredQty { get; set; }
    /// <summary>Sell price per unit. If 0, falls back to the delivery order line price.</summary>
    public decimal UnitPrice { get; set; }
}

// ── Hub stock acceptance ───────────────────────────────────────────────────────
public class HubAcceptAllocationRequest
{
    public List<HubAcceptAllocationLine> Lines { get; set; } = new();
}

public class HubAcceptAllocationLine
{
    public string SpeciesId { get; set; } = "";
    /// <summary>Qty hub staff physically counted. Must be ≤ allocated qty.</summary>
    public int AcceptedQty { get; set; }
}

// ── Patch allocation payment type (admin/owner correction) ────────────────────
public class PatchAllocationPaymentRequest
{
    /// <summary>Cash | EFT | Credit | CardMachine</summary>
    public string PaymentType { get; set; } = "";

    /// <summary>S3 key of the proof-of-payment uploaded before calling this endpoint. Optional.</summary>
    public string? ReceiptS3Key { get; set; }
}

// ── Roadside sales ─────────────────────────────────────────────────────────────
public class SetRoadsideSalesRequest
{
    public List<RoadsaleLineRequest> Lines { get; set; } = new();
}

public class RoadsaleLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string PaymentType { get; set; } = ""; // Cash, EFT
}

public class RoadsaleLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string PaymentType { get; set; } = "";
}

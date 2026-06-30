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
    public List<CollectionDeliveryAllocation> DeliveryAllocations { get; set; } = new();
    public List<CollectionRoadsaleLine> RoadsideSales { get; set; } = new();
    public bool ShortfallFlagged { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CollectionDeliveryAllocation
{
    public string DeliveryOrderId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public List<CollectionAllocationLine> Lines { get; set; } = new();

    // Hub-direct allocations only — tracks whether hub staff has physically accepted the stock
    public string HubAcceptanceStatus { get; set; } = ""; // "" | "Accepted"
    public DateTime? HubAcceptedAt { get; set; }
}

public class CollectionAllocationLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>For HUB allocations: qty hub staff physically counted and accepted. 0 = not yet accepted.</summary>
    public int AcceptedQty { get; set; }

    /// <summary>True if this allocation deducted QtyOnHandHub at creation time (hub-internal supplier,
    /// or an external-supplier allocation added after the collection was already hub-confirmed).
    /// Edit/remove logic must check this flag rather than re-deriving it, since the collection's
    /// current status/supplier can't be relied on to reflect what was true when this line was made.</summary>
    public bool OnHandDeducted { get; set; }
}

public class CollectionRoadsaleLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public string PaymentType { get; set; } = ""; // Cash, EFT
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

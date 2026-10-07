namespace KwaWicks.Domain.Entities;

public class InboundShipment
{
    public string InboundShipmentId { get; set; } = Guid.NewGuid().ToString();
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string HubId { get; set; } = "hub-001";

    /// <summary>Open → Dispatched → Received → Completed</summary>
    public string Status { get; set; } = "Open";

    public string TransporterName { get; set; } = "";
    public string TransporterContact { get; set; } = "";
    public string VehicleReg { get; set; } = "";

    public string Notes { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";

    public DateTime? ExpectedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<InboundShipmentLine> Lines { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class InboundShipmentLine
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Actual qty counted by hub staff at check-in.</summary>
    public int ReceivedQty { get; set; }

    /// <summary>Reason for shortfall or damage, if any.</summary>
    public string ShortfallNotes { get; set; } = "";
}

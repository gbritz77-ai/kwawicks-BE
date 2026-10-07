namespace KwaWicks.Application.DTOs;

public class CreateInboundShipmentRequest
{
    public string SupplierId { get; set; } = "";
    public string TransporterName { get; set; } = "";
    public string TransporterContact { get; set; } = "";
    public string VehicleReg { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? ExpectedAt { get; set; }
    public List<InboundShipmentLineRequest> Lines { get; set; } = new();
}

public class InboundShipmentLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal UnitCost { get; set; }
}

public class ReceiveInboundShipmentRequest
{
    public List<ReceiveInboundShipmentLine> Lines { get; set; } = new();
}

public class ReceiveInboundShipmentLine
{
    public string SpeciesId { get; set; } = "";
    public int ReceivedQty { get; set; }
    public string ShortfallNotes { get; set; } = "";
}

public class InboundShipmentResponse
{
    public string InboundShipmentId { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string HubId { get; set; } = "";
    public string Status { get; set; } = "";
    public string TransporterName { get; set; } = "";
    public string TransporterContact { get; set; } = "";
    public string VehicleReg { get; set; } = "";
    public string Notes { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public DateTime? ExpectedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<InboundShipmentLineResponse> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InboundShipmentLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal UnitCost { get; set; }
    public int ReceivedQty { get; set; }
    public string ShortfallNotes { get; set; } = "";
}

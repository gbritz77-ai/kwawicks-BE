namespace KwaWicks.Application.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public class CreateDeliveryRunRequest
{
    public string HubId { get; set; } = "hub-001";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class AddDeliveryRunAllocationRequest
{
    public string ClientId { get; set; } = "";
    public List<DeliveryRunAllocationLineRequest> Lines { get; set; } = new();
}

public class DeliveryRunAllocationLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

// ── Reallocate surplus stock from one active delivery to another ──────────────
public class ReallocateDeliveryRunStockRequest
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }

    /// <summary>Set this to move stock onto an existing, not-yet-delivered allocation in the same run.</summary>
    public string? ToDeliveryOrderId { get; set; }

    /// <summary>Set this (instead of ToDeliveryOrderId) to spin up a brand-new delivery for a client not yet on this run.</summary>
    public string? ToClientId { get; set; }

    /// <summary>Unit price for the new line. Falls back to the source line's price if 0.</summary>
    public decimal UnitPrice { get; set; }
}

public class ConfirmDeliveryRunDeliveryRequest
{
    public List<DeliveryRunConfirmLine> Lines { get; set; } = new();
    public string PaymentType { get; set; } = "";
}

public class DeliveryRunConfirmLine
{
    public string SpeciesId { get; set; } = "";
    public int DeliveredQty { get; set; }
    public decimal UnitPrice { get; set; }
}

// ── Response ──────────────────────────────────────────────────────────────────

public class DeliveryRunAllocationLineDto
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public int DeliveredQty { get; set; }
}

public class DeliveryRunAllocationDto
{
    public string DeliveryOrderId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string DeliveryStatus { get; set; } = "";
    public string InvoiceId { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public List<DeliveryRunAllocationLineDto> Lines { get; set; } = new();
}

public class DeliveryRunResponse
{
    public string DeliveryRunId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<DeliveryRunAllocationDto> Allocations { get; set; } = new();
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

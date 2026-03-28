namespace KwaWicks.Application.DTOs;

public class HubRequestDto
{
    public string HubRequestId { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "";
    public string ActionedBy { get; set; } = "";
    public string ActionNotes { get; set; } = "";
    public string LinkedOrderId { get; set; } = "";
    public string LinkedOrderType { get; set; } = "";
    public string LinkedOrderRef { get; set; } = "";
    public string? WhatsAppError { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ActionedAtUtc { get; set; }
}

public class CreateHubRequestRequest
{
    public string Message { get; set; } = "";
}

public class ActionHubRequestRequest
{
    public string ActionNotes { get; set; } = "";
    public string LinkedOrderId { get; set; } = "";
    public string LinkedOrderType { get; set; } = "";  // DeliveryOrder | ProcurementOrder | Other
    public string LinkedOrderRef { get; set; } = "";
}

namespace KwaWicks.Domain.Entities;

public class HubRequest
{
    public string HubRequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string RequestedBy { get; set; } = "";     // Cognito username of requester
    public string Message { get; set; } = "";          // The order/action request
    public string Status { get; set; } = "Pending";   // Pending | Actioned | Cancelled
    public string ActionedBy { get; set; } = "";
    public string ActionNotes { get; set; } = "";
    public string LinkedOrderId { get; set; } = "";    // ID of the order created from this request
    public string LinkedOrderType { get; set; } = ""; // DeliveryOrder | ProcurementOrder | Other
    public string LinkedOrderRef { get; set; } = "";  // Human-readable reference (e.g. DO#abc123)
    public string WhatsAppError { get; set; } = "";   // Non-empty if WhatsApp notification failed
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActionedAtUtc { get; set; }
}

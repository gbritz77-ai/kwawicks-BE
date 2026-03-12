namespace KwaWicks.Domain.Entities;

public class HubTask
{
    public string HubTaskId { get; set; } = Guid.NewGuid().ToString();
    public string HubId { get; set; } = "";

    public string Type { get; set; } = "Invoice";
    public string Status { get; set; } = "Open";

    public string InvoiceId { get; set; } = "";
    public string DeliveryOrderId { get; set; } = "";

    public string Title { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
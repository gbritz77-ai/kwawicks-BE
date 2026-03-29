namespace KwaWicks.Domain.Entities;

public class AppSettings
{
    public string HubWhatsAppNumber { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";
}

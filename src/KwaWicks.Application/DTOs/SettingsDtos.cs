namespace KwaWicks.Application.DTOs;

public class AppSettingsDto
{
    public string HubWhatsAppNumber { get; set; } = "";
    public DateTime? UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = "";
}

public class UpdateAppSettingsRequest
{
    public string HubWhatsAppNumber { get; set; } = "";
}

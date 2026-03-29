using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repo;

    public SettingsService(ISettingsRepository repo)
    {
        _repo = repo;
    }

    public async Task<AppSettingsDto> GetAsync(CancellationToken ct)
    {
        var s = await _repo.GetAsync(ct);
        if (s == null)
            return new AppSettingsDto
            {
                HubWhatsAppNumber = Environment.GetEnvironmentVariable("HUB_WHATSAPP_NUMBER") ?? ""
            };
        return Map(s);
    }

    public async Task<AppSettingsDto> UpdateAsync(UpdateAppSettingsRequest request, string updatedBy, CancellationToken ct)
    {
        var phone = (request.HubWhatsAppNumber ?? "").Trim();
        var settings = new AppSettings
        {
            HubWhatsAppNumber = phone,
            UpdatedBy = updatedBy,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var saved = await _repo.SaveAsync(settings, ct);
        return Map(saved);
    }

    private static AppSettingsDto Map(AppSettings s) => new()
    {
        HubWhatsAppNumber = s.HubWhatsAppNumber,
        UpdatedAtUtc = s.UpdatedAtUtc,
        UpdatedBy = s.UpdatedBy,
    };
}

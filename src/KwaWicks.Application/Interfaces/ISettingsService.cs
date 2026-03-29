using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface ISettingsService
{
    Task<AppSettingsDto> GetAsync(CancellationToken ct);
    Task<AppSettingsDto> UpdateAsync(UpdateAppSettingsRequest request, string updatedBy, CancellationToken ct);
}

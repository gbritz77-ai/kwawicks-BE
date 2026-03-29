using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ISettingsRepository
{
    Task<AppSettings?> GetAsync(CancellationToken ct);
    Task<AppSettings> SaveAsync(AppSettings settings, CancellationToken ct);
}

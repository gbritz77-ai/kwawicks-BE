using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IDeliveryRunRepository
{
    Task<DeliveryRun> CreateAsync(DeliveryRun run, CancellationToken ct);
    Task<DeliveryRun?> GetAsync(string id, CancellationToken ct);
    Task<DeliveryRun> UpdateAsync(DeliveryRun run, CancellationToken ct);
    Task<List<DeliveryRun>> ListAsync(string? driverId, string? status, CancellationToken ct);
}

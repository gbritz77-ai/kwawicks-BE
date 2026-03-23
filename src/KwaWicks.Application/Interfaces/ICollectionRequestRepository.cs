using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ICollectionRequestRepository
{
    Task<CollectionRequest> CreateAsync(CollectionRequest cr, CancellationToken ct = default);
    Task<CollectionRequest?> GetAsync(string id, CancellationToken ct = default);
    Task<List<CollectionRequest>> ListAsync(string? driverId = null, string? status = null, string? procurementOrderId = null, CancellationToken ct = default);
    Task<CollectionRequest> UpdateAsync(CollectionRequest cr, CancellationToken ct = default);
}

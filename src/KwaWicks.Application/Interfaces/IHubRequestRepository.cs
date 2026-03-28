using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IHubRequestRepository
{
    Task<HubRequest> CreateAsync(HubRequest request, CancellationToken ct);
    Task<HubRequest?> GetAsync(string hubRequestId, CancellationToken ct);
    Task<List<HubRequest>> ListAsync(string? status, CancellationToken ct);
    Task<HubRequest> UpdateAsync(HubRequest request, CancellationToken ct);
}

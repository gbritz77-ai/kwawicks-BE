using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IHubRequestService
{
    Task<HubRequestDto> CreateAsync(CreateHubRequestRequest request, string requestedBy, CancellationToken ct);
    Task<List<HubRequestDto>> ListAsync(string? status, CancellationToken ct);
    Task<HubRequestDto> GetAsync(string hubRequestId, CancellationToken ct);
    Task<HubRequestDto> ActionAsync(string hubRequestId, ActionHubRequestRequest request, string actionedBy, CancellationToken ct);
    Task<HubRequestDto> CancelAsync(string hubRequestId, string cancelledBy, CancellationToken ct);
}

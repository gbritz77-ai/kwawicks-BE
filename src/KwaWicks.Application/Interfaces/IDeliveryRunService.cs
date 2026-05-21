using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IDeliveryRunService
{
    Task<DeliveryRunResponse> CreateAsync(CreateDeliveryRunRequest request, CancellationToken ct);
    Task<DeliveryRunResponse?> GetAsync(string id, CancellationToken ct);
    Task<List<DeliveryRunResponse>> ListAsync(string? driverId, string? status, CancellationToken ct);
    Task<DeliveryRunResponse> AddAllocationAsync(string id, AddDeliveryRunAllocationRequest request, CancellationToken ct);
    Task<DeliveryRunResponse> RemoveAllocationAsync(string id, string deliveryOrderId, CancellationToken ct);
    Task<DeliveryRunResponse> DispatchAsync(string id, CancellationToken ct);
    Task<DeliveryRunResponse> ConfirmDeliveryAsync(string id, string deliveryOrderId, ConfirmDeliveryRunDeliveryRequest request, CancellationToken ct);
}

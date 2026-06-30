using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IDeliveryRunService
{
    Task<DeliveryRunResponse> CreateAsync(CreateDeliveryRunRequest request, CancellationToken ct);
    Task<DeliveryRunResponse?> GetAsync(string id, CancellationToken ct);
    Task<List<DeliveryRunResponse>> ListAsync(string? driverId, string? status, CancellationToken ct);
    Task<DeliveryRunResponse> AddAllocationAsync(string id, AddDeliveryRunAllocationRequest request, CancellationToken ct);
    Task<DeliveryRunResponse> RemoveAllocationAsync(string id, string deliveryOrderId, CancellationToken ct);
    /// <summary>Owner/Admin/Finance: move stock from one not-yet-delivered allocation to another while the
    /// driver is out — no hub stock movement, the goods are already physically on the truck.</summary>
    Task<DeliveryRunResponse> ReallocateStockAsync(string id, string fromDeliveryOrderId, ReallocateDeliveryRunStockRequest request, CancellationToken ct);
    Task<DeliveryRunResponse> DispatchAsync(string id, CancellationToken ct);
    Task<DeliveryRunResponse> ConfirmDeliveryAsync(string id, string deliveryOrderId, ConfirmDeliveryRunDeliveryRequest request, CancellationToken ct);
}

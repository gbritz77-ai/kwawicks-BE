using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IInboundShipmentService
{
    Task<InboundShipmentResponse> CreateAsync(CreateInboundShipmentRequest request, string createdByUserId, CancellationToken ct = default);
    Task<InboundShipmentResponse?> GetAsync(string id, CancellationToken ct = default);
    Task<List<InboundShipmentResponse>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default);
    Task<InboundShipmentResponse> UpdateAsync(string id, CreateInboundShipmentRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task MarkDispatchedAsync(string id, CancellationToken ct = default);
    Task ReceiveAsync(string id, ReceiveInboundShipmentRequest request, CancellationToken ct = default);
}

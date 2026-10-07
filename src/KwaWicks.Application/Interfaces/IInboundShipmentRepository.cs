using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IInboundShipmentRepository
{
    Task<InboundShipment> CreateAsync(InboundShipment shipment, CancellationToken ct = default);
    Task<InboundShipment?> GetAsync(string id, CancellationToken ct = default);
    Task<List<InboundShipment>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default);
    Task<InboundShipment> UpdateAsync(InboundShipment shipment, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

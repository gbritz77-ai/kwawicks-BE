using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IProcurementOrderRepository
{
    Task<ProcurementOrder> CreateAsync(ProcurementOrder order, CancellationToken ct = default);
    Task<ProcurementOrder?> GetAsync(string id, CancellationToken ct = default);
    Task<List<ProcurementOrder>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default);
    Task<ProcurementOrder> UpdateAsync(ProcurementOrder order, CancellationToken ct = default);
}

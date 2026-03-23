using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier> CreateAsync(Supplier supplier, CancellationToken ct = default);
    Task<Supplier?> GetAsync(string supplierId, CancellationToken ct = default);
    Task<List<Supplier>> ListAsync(CancellationToken ct = default);
    Task<Supplier> UpdateAsync(Supplier supplier, CancellationToken ct = default);
    Task DeleteAsync(string supplierId, CancellationToken ct = default);
}

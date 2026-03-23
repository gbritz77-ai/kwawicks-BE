using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface ISupplierService
{
    Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<SupplierResponse?> GetAsync(string supplierId, CancellationToken ct = default);
    Task<List<SupplierResponse>> ListAsync(CancellationToken ct = default);
    Task<SupplierResponse> UpdateAsync(string supplierId, UpdateSupplierRequest request, CancellationToken ct = default);
    Task DeleteAsync(string supplierId, CancellationToken ct = default);
}

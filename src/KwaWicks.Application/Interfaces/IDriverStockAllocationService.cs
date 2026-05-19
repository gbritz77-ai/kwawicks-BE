using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IDriverStockAllocationService
{
    Task<DriverStockAllocationResponse> CreateAsync(CreateDriverStockAllocationRequest request, CancellationToken ct = default);
    Task<DriverStockAllocationResponse?> GetAsync(string id, CancellationToken ct = default);
    Task<List<DriverStockAllocationResponse>> ListAsync(string? driverId, string? status, CancellationToken ct = default);
    Task<DriverStockAllocationResponse> RecordSaleAsync(string id, RecordDriverSaleRequest request, CancellationToken ct = default);
    Task<DriverStockAllocationResponse> CompleteAsync(string id, CancellationToken ct = default);
    Task<DriverStockAllocationResponse> CancelAsync(string id, CancellationToken ct = default);
}

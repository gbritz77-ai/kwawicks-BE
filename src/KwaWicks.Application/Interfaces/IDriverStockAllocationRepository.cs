using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IDriverStockAllocationRepository
{
    Task<DriverStockAllocation> CreateAsync(DriverStockAllocation allocation, CancellationToken ct = default);
    Task<DriverStockAllocation?> GetAsync(string id, CancellationToken ct = default);
    Task<List<DriverStockAllocation>> ListAsync(string? driverId, string? status, CancellationToken ct = default);
    Task<DriverStockAllocation> UpdateAsync(DriverStockAllocation allocation, CancellationToken ct = default);
}

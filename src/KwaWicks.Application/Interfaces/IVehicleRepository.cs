using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle> CreateAsync(Vehicle vehicle, CancellationToken ct);
    Task<Vehicle?> GetAsync(string vehicleId, CancellationToken ct);
    Task<Vehicle> UpdateAsync(Vehicle vehicle, CancellationToken ct);
    Task<List<Vehicle>> ListAsync(CancellationToken ct);
}

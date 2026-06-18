using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class VehicleService
{
    private readonly IVehicleRepository _repo;

    public VehicleService(IVehicleRepository repo) => _repo = repo;

    public async Task<VehicleDto> CreateAsync(CreateVehicleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FleetNumber))
            throw new ArgumentException("FleetNumber is required.");

        var vehicle = new Vehicle
        {
            FleetNumber          = req.FleetNumber.Trim(),
            Registration         = req.Registration?.Trim() ?? "",
            Make                 = req.Make?.Trim() ?? "",
            Model                = req.Model?.Trim() ?? "",
            Year                 = req.Year,
            FuelType             = req.FuelType ?? "diesel",
            OdoType              = req.OdoType ?? "km",
            OdometerKm           = req.OdometerKm,
            ExpectedConsumption  = req.ExpectedConsumption,
            LicenceExpiry        = req.LicenceExpiry,
            LicenceRemindDays    = req.LicenceRemindDays,
            LastServiceOdo       = req.LastServiceOdo,
            ServiceInterval      = req.ServiceInterval,
            ServiceNotifyBefore  = req.ServiceNotifyBefore,
            Notes                = req.Notes?.Trim() ?? "",
        };

        await _repo.CreateAsync(vehicle, ct);
        return ToDto(vehicle);
    }

    public async Task<VehicleDto?> GetAsync(string vehicleId, CancellationToken ct)
    {
        var v = await _repo.GetAsync(vehicleId, ct);
        return v is null ? null : ToDto(v);
    }

    public async Task<List<VehicleDto>> ListAsync(string? search, CancellationToken ct)
    {
        var all = await _repo.ListAsync(ct);
        var active = all.Where(v => v.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            active = active.Where(v =>
                v.FleetNumber.ToLowerInvariant().Contains(q) ||
                v.Registration.ToLowerInvariant().Contains(q) ||
                v.Make.ToLowerInvariant().Contains(q) ||
                v.Model.ToLowerInvariant().Contains(q));
        }

        return active.OrderBy(v => v.FleetNumber).Select(ToDto).ToList();
    }

    public async Task<VehicleDto?> UpdateAsync(string vehicleId, UpdateVehicleRequest req, CancellationToken ct)
    {
        var v = await _repo.GetAsync(vehicleId, ct);
        if (v is null) return null;

        if (req.FleetNumber is not null) v.FleetNumber = req.FleetNumber.Trim();
        if (req.Registration is not null) v.Registration = req.Registration.Trim();
        if (req.Make is not null) v.Make = req.Make.Trim();
        if (req.Model is not null) v.Model = req.Model.Trim();
        if (req.Year.HasValue) v.Year = req.Year;
        if (req.FuelType is not null) v.FuelType = req.FuelType;
        if (req.OdoType is not null) v.OdoType = req.OdoType;
        if (req.OdometerKm.HasValue) v.OdometerKm = req.OdometerKm;
        if (req.ExpectedConsumption.HasValue) v.ExpectedConsumption = req.ExpectedConsumption;
        if (req.LicenceExpiry is not null) v.LicenceExpiry = req.LicenceExpiry;
        if (req.LicenceRemindDays.HasValue) v.LicenceRemindDays = req.LicenceRemindDays;
        if (req.LastServiceOdo.HasValue) v.LastServiceOdo = req.LastServiceOdo;
        if (req.ServiceInterval.HasValue) v.ServiceInterval = req.ServiceInterval;
        if (req.ServiceNotifyBefore.HasValue) v.ServiceNotifyBefore = req.ServiceNotifyBefore;
        if (req.Notes is not null) v.Notes = req.Notes.Trim();
        if (req.IsActive.HasValue) v.IsActive = req.IsActive.Value;

        v.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.UpdateAsync(v, ct);
        return ToDto(v);
    }

    public async Task<bool> DeactivateAsync(string vehicleId, CancellationToken ct)
    {
        var v = await _repo.GetAsync(vehicleId, ct);
        if (v is null) return false;
        v.IsActive = false;
        v.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.UpdateAsync(v, ct);
        return true;
    }

    private static VehicleDto ToDto(Vehicle v) => new()
    {
        VehicleId           = v.VehicleId,
        FleetNumber         = v.FleetNumber,
        Registration        = v.Registration,
        Make                = v.Make,
        Model               = v.Model,
        Year                = v.Year,
        FuelType            = v.FuelType,
        OdoType             = v.OdoType,
        OdometerKm          = v.OdometerKm,
        ExpectedConsumption = v.ExpectedConsumption,
        LicenceExpiry       = v.LicenceExpiry,
        LicenceRemindDays   = v.LicenceRemindDays,
        LastServiceOdo      = v.LastServiceOdo,
        ServiceInterval     = v.ServiceInterval,
        ServiceNotifyBefore = v.ServiceNotifyBefore,
        Notes               = v.Notes,
        IsActive            = v.IsActive,
        CreatedAtUtc        = v.CreatedAtUtc,
        UpdatedAtUtc        = v.UpdatedAtUtc,
    };
}

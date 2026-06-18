namespace KwaWicks.Application.DTOs;

public class VehicleDto
{
    public string VehicleId { get; set; } = "";
    public string FleetNumber { get; set; } = "";
    public string Registration { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int? Year { get; set; }
    public string FuelType { get; set; } = "";
    public string OdoType { get; set; } = "km";
    public decimal? OdometerKm { get; set; }
    public decimal? ExpectedConsumption { get; set; }
    public string? LicenceExpiry { get; set; }
    public int? LicenceRemindDays { get; set; }
    public decimal? LastServiceOdo { get; set; }
    public decimal? ServiceInterval { get; set; }
    public decimal? ServiceNotifyBefore { get; set; }
    public string Notes { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class CreateVehicleRequest
{
    public string FleetNumber { get; set; } = "";
    public string? Registration { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string FuelType { get; set; } = "diesel";
    public string OdoType { get; set; } = "km";
    public decimal? OdometerKm { get; set; }
    public decimal? ExpectedConsumption { get; set; }
    public string? LicenceExpiry { get; set; }
    public int? LicenceRemindDays { get; set; }
    public decimal? LastServiceOdo { get; set; }
    public decimal? ServiceInterval { get; set; }
    public decimal? ServiceNotifyBefore { get; set; }
    public string? Notes { get; set; }
}

public class UpdateVehicleRequest
{
    public string? FleetNumber { get; set; }
    public string? Registration { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? FuelType { get; set; }
    public string? OdoType { get; set; }
    public decimal? OdometerKm { get; set; }
    public decimal? ExpectedConsumption { get; set; }
    public string? LicenceExpiry { get; set; }
    public int? LicenceRemindDays { get; set; }
    public decimal? LastServiceOdo { get; set; }
    public decimal? ServiceInterval { get; set; }
    public decimal? ServiceNotifyBefore { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

namespace KwaWicks.Domain.Entities;

public class Vehicle
{
    public string VehicleId { get; set; } = Guid.NewGuid().ToString("N");
    public string FleetNumber { get; set; } = "";
    public string Registration { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int? Year { get; set; }
    public string FuelType { get; set; } = "diesel"; // diesel | petrol | electric | hybrid
    public string OdoType { get; set; } = "km";      // km | hrs
    public decimal? OdometerKm { get; set; }
    public decimal? ExpectedConsumption { get; set; }

    // Licence
    public string? LicenceExpiry { get; set; }       // yyyy-MM-dd
    public int? LicenceRemindDays { get; set; }

    // Service intervals
    public decimal? LastServiceOdo { get; set; }
    public decimal? ServiceInterval { get; set; }
    public decimal? ServiceNotifyBefore { get; set; }

    public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<LicenceHistoryEntry> LicenceHistory { get; set; } = new();
}

public class LicenceHistoryEntry
{
    public string? PreviousExpiry { get; set; }
    public string NewExpiry { get; set; } = "";
    public decimal? Cost { get; set; }
    public DateTime RenewedAt { get; set; } = DateTime.UtcNow;
    public string RenewedBy { get; set; } = "";
}

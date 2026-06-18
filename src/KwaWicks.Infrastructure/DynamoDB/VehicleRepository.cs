using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class VehicleRepository : IVehicleRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public VehicleRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"VEHICLE#{id}";
    private const string SkProfile = "PROFILE";

    public async Task<Vehicle> CreateAsync(Vehicle vehicle, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(vehicle),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return vehicle;
    }

    public async Task<Vehicle?> GetAsync(string vehicleId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(vehicleId) },
                ["SK"] = new AttributeValue { S = SkProfile }
            }
        }, ct);
        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<Vehicle> UpdateAsync(Vehicle vehicle, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(vehicle)
        }, ct);
        return vehicle;
    }

    public async Task<List<Vehicle>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new AttributeValue { S = "Vehicle" }
            }
        };

        var result = new List<Vehicle>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result;
    }

    private static Dictionary<string, AttributeValue> ToItem(Vehicle v)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = Pk(v.VehicleId) },
            ["SK"] = new AttributeValue { S = SkProfile },
            ["EntityType"] = new AttributeValue { S = "Vehicle" },
            ["VehicleId"] = new AttributeValue { S = v.VehicleId },
            ["FleetNumber"] = new AttributeValue { S = v.FleetNumber },
            ["Registration"] = new AttributeValue { S = v.Registration },
            ["Make"] = new AttributeValue { S = v.Make },
            ["Model"] = new AttributeValue { S = v.Model },
            ["FuelType"] = new AttributeValue { S = v.FuelType },
            ["OdoType"] = new AttributeValue { S = v.OdoType },
            ["Notes"] = new AttributeValue { S = v.Notes },
            ["IsActive"] = new AttributeValue { BOOL = v.IsActive },
            ["CreatedAtUtc"] = new AttributeValue { S = v.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) },
            ["UpdatedAtUtc"] = new AttributeValue { S = v.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture) },
        };

        if (v.Year.HasValue) item["Year"] = new AttributeValue { N = v.Year.Value.ToString() };
        if (v.OdometerKm.HasValue) item["OdometerKm"] = new AttributeValue { N = v.OdometerKm.Value.ToString(CultureInfo.InvariantCulture) };
        if (v.ExpectedConsumption.HasValue) item["ExpectedConsumption"] = new AttributeValue { N = v.ExpectedConsumption.Value.ToString(CultureInfo.InvariantCulture) };
        if (v.LicenceExpiry is not null) item["LicenceExpiry"] = new AttributeValue { S = v.LicenceExpiry };
        if (v.LicenceRemindDays.HasValue) item["LicenceRemindDays"] = new AttributeValue { N = v.LicenceRemindDays.Value.ToString() };
        if (v.LastServiceOdo.HasValue) item["LastServiceOdo"] = new AttributeValue { N = v.LastServiceOdo.Value.ToString(CultureInfo.InvariantCulture) };
        if (v.ServiceInterval.HasValue) item["ServiceInterval"] = new AttributeValue { N = v.ServiceInterval.Value.ToString(CultureInfo.InvariantCulture) };
        if (v.ServiceNotifyBefore.HasValue) item["ServiceNotifyBefore"] = new AttributeValue { N = v.ServiceNotifyBefore.Value.ToString(CultureInfo.InvariantCulture) };

        return item;
    }

    private static Vehicle FromItem(Dictionary<string, AttributeValue> item) => new()
    {
        VehicleId           = item.TryGetValue("VehicleId", out var vid) ? vid.S ?? "" : "",
        FleetNumber         = item.TryGetValue("FleetNumber", out var fn) ? fn.S ?? "" : "",
        Registration        = item.TryGetValue("Registration", out var reg) ? reg.S ?? "" : "",
        Make                = item.TryGetValue("Make", out var mk) ? mk.S ?? "" : "",
        Model               = item.TryGetValue("Model", out var mo) ? mo.S ?? "" : "",
        FuelType            = item.TryGetValue("FuelType", out var ft) ? ft.S ?? "diesel" : "diesel",
        OdoType             = item.TryGetValue("OdoType", out var ot) ? ot.S ?? "km" : "km",
        Notes               = item.TryGetValue("Notes", out var no) ? no.S ?? "" : "",
        IsActive            = item.TryGetValue("IsActive", out var ia) && ia.IsBOOLSet && ia.BOOL == true,
        Year                = item.TryGetValue("Year", out var yr) && int.TryParse(yr.N, out var yrv) ? yrv : null,
        OdometerKm          = item.TryGetValue("OdometerKm", out var odo) && decimal.TryParse(odo.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var odov) ? odov : null,
        ExpectedConsumption = item.TryGetValue("ExpectedConsumption", out var ec) && decimal.TryParse(ec.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var ecv) ? ecv : null,
        LicenceExpiry       = item.TryGetValue("LicenceExpiry", out var le) ? le.S : null,
        LicenceRemindDays   = item.TryGetValue("LicenceRemindDays", out var lrd) && int.TryParse(lrd.N, out var lrdv) ? lrdv : null,
        LastServiceOdo      = item.TryGetValue("LastServiceOdo", out var ls) && decimal.TryParse(ls.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var lsv) ? lsv : null,
        ServiceInterval     = item.TryGetValue("ServiceInterval", out var si) && decimal.TryParse(si.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var siv) ? siv : null,
        ServiceNotifyBefore = item.TryGetValue("ServiceNotifyBefore", out var snb) && decimal.TryParse(snb.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var snbv) ? snbv : null,
        CreatedAtUtc        = item.TryGetValue("CreatedAtUtc", out var ca) ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        UpdatedAtUtc        = item.TryGetValue("UpdatedAtUtc", out var ua) ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
    };
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class SettingsRepository : ISettingsRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    // Single item — one row holds all app settings
    private const string Pk = "SETTINGS#APP";
    private const string Sk = "HUB";

    public SettingsRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    public async Task<AppSettings?> GetAsync(CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = Pk },
                ["SK"] = new() { S = Sk }
            }
        }, ct);

        if (resp.Item == null || resp.Item.Count == 0)
            return null;

        return FromItem(resp.Item);
    }

    public async Task<AppSettings> SaveAsync(AppSettings settings, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(settings)
        }, ct);
        return settings;
    }

    private static Dictionary<string, AttributeValue> ToItem(AppSettings s) => new()
    {
        ["PK"] = new() { S = Pk },
        ["SK"] = new() { S = Sk },
        ["EntityType"] = new() { S = "AppSettings" },
        ["HubWhatsAppNumber"] = new() { S = s.HubWhatsAppNumber ?? "" },
        ["UpdatedBy"] = new() { S = s.UpdatedBy ?? "" },
        ["UpdatedAtUtc"] = new() { S = s.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
    };

    private static AppSettings FromItem(Dictionary<string, AttributeValue> i) => new()
    {
        HubWhatsAppNumber = i.TryGetValue("HubWhatsAppNumber", out var hw) ? hw.S ?? "" : "",
        UpdatedBy = i.TryGetValue("UpdatedBy", out var ub) ? ub.S ?? "" : "",
        UpdatedAtUtc = i.TryGetValue("UpdatedAtUtc", out var ua)
            ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow,
    };
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class StockLossRepository : IStockLossRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public StockLossRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string lossId) => $"STOCKLOSS#{lossId}";
    private const string SkMeta = "META";

    public async Task<StockLoss> AddAsync(StockLoss loss, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(loss),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return loss;
    }

    public async Task<List<StockLoss>> ListAsync(
        string? speciesId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new() { S = "StockLoss" }
        };

        if (!string.IsNullOrWhiteSpace(speciesId))
        {
            filterParts.Add("SpeciesId = :sid");
            values[":sid"] = new() { S = speciesId };
        }
        if (from.HasValue)
        {
            filterParts.Add("CreatedAt >= :from");
            values[":from"] = new() { S = from.Value.ToString("O", CultureInfo.InvariantCulture) };
        }
        if (to.HasValue)
        {
            filterParts.Add("CreatedAt <= :to");
            values[":to"] = new() { S = to.Value.AddDays(1).ToString("O", CultureInfo.InvariantCulture) };
        }

        var resp = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = string.Join(" AND ", filterParts),
            ExpressionAttributeValues = values,
        }, ct);

        return resp.Items
            .Select(FromItem)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(StockLoss l) => new()
    {
        ["PK"]               = new() { S = Pk(l.LossId) },
        ["SK"]               = new() { S = SkMeta },
        ["EntityType"]       = new() { S = "StockLoss" },
        ["LossId"]           = new() { S = l.LossId },
        ["SpeciesId"]        = new() { S = l.SpeciesId },
        ["SpeciesName"]      = new() { S = l.SpeciesName },
        ["Qty"]              = new() { N = l.Qty.ToString() },
        ["Notes"]            = new() { S = l.Notes },
        ["RecordedByUserId"] = new() { S = l.RecordedByUserId },
        ["CreatedAt"]        = new() { S = l.CreatedAt.ToString("O") },
    };

    private static StockLoss FromItem(Dictionary<string, AttributeValue> item)
    {
        static string Str(Dictionary<string, AttributeValue> d, string k) =>
            d.TryGetValue(k, out var v) ? v.S ?? "" : "";
        static int Int(Dictionary<string, AttributeValue> d, string k) =>
            d.TryGetValue(k, out var v) && int.TryParse(v.N, out var n) ? n : 0;
        static DateTime Dt(Dictionary<string, AttributeValue> d, string k) =>
            d.TryGetValue(k, out var v) && DateTime.TryParse(v.S, null, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.MinValue;

        return new StockLoss
        {
            LossId           = Str(item, "LossId"),
            SpeciesId        = Str(item, "SpeciesId"),
            SpeciesName      = Str(item, "SpeciesName"),
            Qty              = Int(item, "Qty"),
            Notes            = Str(item, "Notes"),
            RecordedByUserId = Str(item, "RecordedByUserId"),
            CreatedAt        = Dt(item,  "CreatedAt"),
        };
    }
}

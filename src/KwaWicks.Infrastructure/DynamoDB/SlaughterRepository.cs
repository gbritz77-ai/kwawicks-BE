using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class SlaughterRepository : ISlaughterRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public SlaughterRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string batchId) => $"SLAUGHTER#{batchId}";
    private const string SkMeta = "META";

    public async Task<SlaughterBatch> CreateAsync(SlaughterBatch batch, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(batch),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return batch;
    }

    public async Task<List<SlaughterBatch>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "begins_with(PK, :p) AND SK = :sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":p"] = new AttributeValue { S = "SLAUGHTER#" },
                [":sk"] = new AttributeValue { S = SkMeta }
            }
        };

        var res = await _ddb.ScanAsync(req, ct);
        return res.Items
            .Select(FromItem)
            .OrderByDescending(b => b.SlaughteredAtUtc)
            .ToList();
    }

    public async Task<SlaughterBatch?> GetAsync(string batchId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(batchId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);

        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(SlaughterBatch b)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"]               = new AttributeValue { S = Pk(b.BatchId) },
            ["SK"]               = new AttributeValue { S = SkMeta },
            ["EntityType"]       = new AttributeValue { S = "SlaughterBatch" },
            ["BatchId"]          = new AttributeValue { S = b.BatchId },
            ["SlaughteredAtUtc"] = new AttributeValue { S = b.SlaughteredAtUtc.ToString("O", CultureInfo.InvariantCulture) },
            ["SourceSpeciesId"]  = new AttributeValue { S = b.SourceSpeciesId },
            ["SourceSpeciesName"]= new AttributeValue { S = b.SourceSpeciesName },
            ["SourceQty"]        = new AttributeValue { N = b.SourceQty.ToString(CultureInfo.InvariantCulture) },
            ["SourceUnitCost"]   = new AttributeValue { N = b.SourceUnitCost.ToString(CultureInfo.InvariantCulture) },
            ["YieldsJson"]       = new AttributeValue { S = JsonSerializer.Serialize(b.Yields) },
        };

        if (!string.IsNullOrWhiteSpace(b.Notes))
            item["Notes"] = new AttributeValue { S = b.Notes };
        if (!string.IsNullOrWhiteSpace(b.CreatedByUserId))
            item["CreatedByUserId"] = new AttributeValue { S = b.CreatedByUserId };

        return item;
    }

    private static SlaughterBatch FromItem(Dictionary<string, AttributeValue> item)
    {
        List<SlaughterYieldLine> yields = new();
        if (item.TryGetValue("YieldsJson", out var yj) && !string.IsNullOrWhiteSpace(yj.S))
            yields = JsonSerializer.Deserialize<List<SlaughterYieldLine>>(yj.S) ?? new();

        return new SlaughterBatch
        {
            BatchId           = item["BatchId"].S,
            SlaughteredAtUtc  = DateTime.Parse(item["SlaughteredAtUtc"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            SourceSpeciesId   = item["SourceSpeciesId"].S,
            SourceSpeciesName = item["SourceSpeciesName"].S,
            SourceQty         = int.Parse(item["SourceQty"].N, CultureInfo.InvariantCulture),
            SourceUnitCost    = decimal.Parse(item["SourceUnitCost"].N, CultureInfo.InvariantCulture),
            Yields            = yields,
            Notes             = item.TryGetValue("Notes", out var n) ? n.S : null,
            CreatedByUserId   = item.TryGetValue("CreatedByUserId", out var u) ? u.S : null,
        };
    }
}

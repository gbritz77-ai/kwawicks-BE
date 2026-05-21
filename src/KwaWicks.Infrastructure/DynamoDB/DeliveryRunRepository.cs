using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class DeliveryRunRepository : IDeliveryRunRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public DeliveryRunRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"DR#{id}";
    private const string SkMeta = "META";

    public async Task<DeliveryRun> CreateAsync(DeliveryRun run, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(run),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return run;
    }

    public async Task<DeliveryRun?> GetAsync(string id, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(id) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);
        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<DeliveryRun> UpdateAsync(DeliveryRun run, CancellationToken ct)
    {
        run.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(run)
        }, ct);
        return run;
    }

    public async Task<List<DeliveryRun>> ListAsync(string? driverId, string? status, CancellationToken ct)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "DeliveryRun" }
        };
        var exprNames = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(driverId))
        {
            filterParts.Add("AssignedDriverId = :driverId");
            values[":driverId"] = new AttributeValue { S = driverId };
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filterParts.Add("#st = :status");
            values[":status"] = new AttributeValue { S = status };
            exprNames["#st"] = "Status";
        }

        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = string.Join(" AND ", filterParts),
            ExpressionAttributeValues = values
        };

        if (exprNames.Count > 0)
            req.ExpressionAttributeNames = exprNames;

        var result = new List<DeliveryRun>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private static Dictionary<string, AttributeValue> ToItem(DeliveryRun run) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(run.DeliveryRunId) },
        ["SK"] = new AttributeValue { S = SkMeta },
        ["EntityType"] = new AttributeValue { S = "DeliveryRun" },
        ["DeliveryRunId"] = new AttributeValue { S = run.DeliveryRunId },
        ["HubId"] = new AttributeValue { S = run.HubId ?? "" },
        ["AssignedDriverId"] = new AttributeValue { S = run.AssignedDriverId ?? "" },
        ["AssignedDriverName"] = new AttributeValue { S = run.AssignedDriverName ?? "" },
        ["Status"] = new AttributeValue { S = run.Status ?? "Open" },
        ["Notes"] = new AttributeValue { S = run.Notes ?? "" },
        ["AllocationsJson"] = new AttributeValue { S = JsonSerializer.Serialize(run.Allocations ?? new()) },
        ["CreatedAtUtc"] = new AttributeValue { S = run.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = run.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
    };

    private static DeliveryRun FromItem(Dictionary<string, AttributeValue> item)
    {
        var allocJson = item.TryGetValue("AllocationsJson", out var aj) ? aj.S : "[]";
        var allocations = JsonSerializer.Deserialize<List<DeliveryRunAllocation>>(allocJson ?? "[]") ?? new();
        return new DeliveryRun
        {
            DeliveryRunId = item.TryGetValue("DeliveryRunId", out var id) ? id.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var h) ? h.S ?? "" : "",
            AssignedDriverId = item.TryGetValue("AssignedDriverId", out var did) ? did.S ?? "" : "",
            AssignedDriverName = item.TryGetValue("AssignedDriverName", out var dn) ? dn.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Open" : "Open",
            Notes = item.TryGetValue("Notes", out var notes) ? notes.S ?? "" : "",
            Allocations = allocations,
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
                ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        };
    }
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class DriverStockAllocationRepository : IDriverStockAllocationRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public DriverStockAllocationRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"DSA#{id}";
    private const string SkMeta = "META";

    public async Task<DriverStockAllocation> CreateAsync(DriverStockAllocation allocation, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(allocation),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return allocation;
    }

    public async Task<DriverStockAllocation?> GetAsync(string id, CancellationToken ct = default)
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

    public async Task<List<DriverStockAllocation>> ListAsync(string? driverId = null, string? status = null, CancellationToken ct = default)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "DriverStockAllocation" }
        };
        var exprNames = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(driverId))
        {
            filterParts.Add("DriverId = :driverId");
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

        var result = new List<DriverStockAllocation>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public async Task<DriverStockAllocation> UpdateAsync(DriverStockAllocation allocation, CancellationToken ct = default)
    {
        allocation.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(allocation)
        }, ct);
        return allocation;
    }

    private static Dictionary<string, AttributeValue> ToItem(DriverStockAllocation allocation) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(allocation.AllocationId) },
        ["SK"] = new AttributeValue { S = SkMeta },
        ["EntityType"] = new AttributeValue { S = "DriverStockAllocation" },
        ["AllocationId"] = new AttributeValue { S = allocation.AllocationId },
        ["DriverId"] = new AttributeValue { S = allocation.DriverId ?? "" },
        ["DriverName"] = new AttributeValue { S = allocation.DriverName ?? "" },
        ["HubId"] = new AttributeValue { S = allocation.HubId ?? "" },
        ["Status"] = new AttributeValue { S = allocation.Status ?? "Active" },
        ["Notes"] = new AttributeValue { S = allocation.Notes ?? "" },
        ["LinesJson"] = new AttributeValue { S = JsonSerializer.Serialize(allocation.Lines ?? new()) },
        ["SalesJson"] = new AttributeValue { S = JsonSerializer.Serialize(allocation.Sales ?? new()) },
        ["CreatedAtUtc"] = new AttributeValue { S = allocation.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = allocation.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
    };

    private static DriverStockAllocation FromItem(Dictionary<string, AttributeValue> item)
    {
        var linesJson = item.TryGetValue("LinesJson", out var lj) ? lj.S : "[]";
        var lines = JsonSerializer.Deserialize<List<DriverStockAllocationLine>>(linesJson ?? "[]") ?? new();

        var salesJson = item.TryGetValue("SalesJson", out var sj) ? sj.S : "[]";
        var sales = JsonSerializer.Deserialize<List<DriverSaleRecord>>(salesJson ?? "[]") ?? new();

        return new DriverStockAllocation
        {
            AllocationId = item.TryGetValue("AllocationId", out var id) ? id.S ?? "" : "",
            DriverId = item.TryGetValue("DriverId", out var did) ? did.S ?? "" : "",
            DriverName = item.TryGetValue("DriverName", out var dn) ? dn.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var h) ? h.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Active" : "Active",
            Notes = item.TryGetValue("Notes", out var notes) ? notes.S ?? "" : "",
            Lines = lines,
            Sales = sales,
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
                ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        };
    }
}

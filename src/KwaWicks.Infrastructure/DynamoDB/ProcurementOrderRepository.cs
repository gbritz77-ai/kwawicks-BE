using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class ProcurementOrderRepository : IProcurementOrderRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public ProcurementOrderRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"PO#{id}";
    private const string SkMeta = "META";

    public async Task<ProcurementOrder> CreateAsync(ProcurementOrder order, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(order),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return order;
    }

    public async Task<ProcurementOrder?> GetAsync(string id, CancellationToken ct = default)
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

    public async Task<List<ProcurementOrder>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "ProcurementOrder" }
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filterParts.Add("#st = :status");
            values[":status"] = new AttributeValue { S = status };
        }

        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filterParts.Add("SupplierId = :sid");
            values[":sid"] = new AttributeValue { S = supplierId };
        }

        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = string.Join(" AND ", filterParts),
            ExpressionAttributeValues = values
        };

        if (!string.IsNullOrWhiteSpace(status))
            req.ExpressionAttributeNames = new Dictionary<string, string> { ["#st"] = "Status" };

        var result = new List<ProcurementOrder>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(id) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);
    }

    public async Task<ProcurementOrder> UpdateAsync(ProcurementOrder order, CancellationToken ct = default)
    {
        order.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(order)
        }, ct);
        return order;
    }

    private static Dictionary<string, AttributeValue> ToItem(ProcurementOrder o) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(o.ProcurementOrderId) },
        ["SK"] = new AttributeValue { S = SkMeta },
        ["EntityType"] = new AttributeValue { S = "ProcurementOrder" },
        ["ProcurementOrderId"] = new AttributeValue { S = o.ProcurementOrderId },
        ["SupplierId"] = new AttributeValue { S = o.SupplierId ?? "" },
        ["SupplierName"] = new AttributeValue { S = o.SupplierName ?? "" },
        ["SupplierOrderReference"] = new AttributeValue { S = o.SupplierOrderReference ?? "" },
        ["Status"] = new AttributeValue { S = o.Status ?? "Draft" },
        ["Notes"] = new AttributeValue { S = o.Notes ?? "" },
        ["InvoiceS3Key"] = new AttributeValue { S = o.InvoiceS3Key ?? "" },
        ["CreatedByUserId"] = new AttributeValue { S = o.CreatedByUserId ?? "" },
        ["LinesJson"] = new AttributeValue { S = JsonSerializer.Serialize(o.Lines ?? new()) },
        ["CreatedAtUtc"] = new AttributeValue { S = o.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = o.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
    };

    private static ProcurementOrder FromItem(Dictionary<string, AttributeValue> item)
    {
        var linesJson = item.TryGetValue("LinesJson", out var lj) ? lj.S : "[]";
        var lines = JsonSerializer.Deserialize<List<ProcurementOrderLine>>(linesJson ?? "[]") ?? new();
        return new ProcurementOrder
        {
            ProcurementOrderId = item.TryGetValue("ProcurementOrderId", out var id) ? id.S ?? "" : "",
            SupplierId = item.TryGetValue("SupplierId", out var sid) ? sid.S ?? "" : "",
            SupplierName = item.TryGetValue("SupplierName", out var sn) ? sn.S ?? "" : "",
            SupplierOrderReference = item.TryGetValue("SupplierOrderReference", out var sor) ? sor.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Draft" : "Draft",
            Notes = item.TryGetValue("Notes", out var notes) ? notes.S ?? "" : "",
            InvoiceS3Key = item.TryGetValue("InvoiceS3Key", out var inv) ? inv.S ?? "" : "",
            CreatedByUserId = item.TryGetValue("CreatedByUserId", out var cbu) ? cbu.S ?? "" : "",
            Lines = lines,
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
                ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        };
    }
}

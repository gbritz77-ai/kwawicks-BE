using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class CollectionRequestRepository : ICollectionRequestRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public CollectionRequestRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"CR#{id}";
    private const string SkMeta = "META";

    public async Task<CollectionRequest> CreateAsync(CollectionRequest cr, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(cr),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return cr;
    }

    public async Task<CollectionRequest?> GetAsync(string id, CancellationToken ct = default)
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

    public async Task<List<CollectionRequest>> ListAsync(string? driverId = null, string? status = null, string? procurementOrderId = null, CancellationToken ct = default)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "CollectionRequest" }
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

        if (!string.IsNullOrWhiteSpace(procurementOrderId))
        {
            filterParts.Add("ProcurementOrderId = :poid");
            values[":poid"] = new AttributeValue { S = procurementOrderId };
        }

        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = string.Join(" AND ", filterParts),
            ExpressionAttributeValues = values
        };

        if (exprNames.Count > 0)
            req.ExpressionAttributeNames = exprNames;

        var result = new List<CollectionRequest>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<CollectionRequest> UpdateAsync(CollectionRequest cr, CancellationToken ct = default)
    {
        cr.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(cr)
        }, ct);
        return cr;
    }

    private static Dictionary<string, AttributeValue> ToItem(CollectionRequest cr) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(cr.CollectionRequestId) },
        ["SK"] = new AttributeValue { S = SkMeta },
        ["EntityType"] = new AttributeValue { S = "CollectionRequest" },
        ["CollectionRequestId"] = new AttributeValue { S = cr.CollectionRequestId },
        ["ProcurementOrderId"] = new AttributeValue { S = cr.ProcurementOrderId ?? "" },
        ["SupplierId"] = new AttributeValue { S = cr.SupplierId ?? "" },
        ["SupplierName"] = new AttributeValue { S = cr.SupplierName ?? "" },
        ["AssignedDriverId"] = new AttributeValue { S = cr.AssignedDriverId ?? "" },
        ["AssignedDriverName"] = new AttributeValue { S = cr.AssignedDriverName ?? "" },
        ["HubId"] = new AttributeValue { S = cr.HubId ?? "" },
        ["Status"] = new AttributeValue { S = cr.Status ?? "Pending" },
        ["Notes"] = new AttributeValue { S = cr.Notes ?? "" },
        ["InvoiceS3Key"] = new AttributeValue { S = cr.InvoiceS3Key ?? "" },
        ["DeliveryNoteS3Key"] = new AttributeValue { S = cr.DeliveryNoteS3Key ?? "" },
        ["LinesJson"] = new AttributeValue { S = JsonSerializer.Serialize(cr.Lines ?? new()) },
        ["CreatedAtUtc"] = new AttributeValue { S = cr.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = cr.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
    };

    private static CollectionRequest FromItem(Dictionary<string, AttributeValue> item)
    {
        var linesJson = item.TryGetValue("LinesJson", out var lj) ? lj.S : "[]";
        var lines = JsonSerializer.Deserialize<List<CollectionRequestLine>>(linesJson ?? "[]") ?? new();
        return new CollectionRequest
        {
            CollectionRequestId = item.TryGetValue("CollectionRequestId", out var id) ? id.S ?? "" : "",
            ProcurementOrderId = item.TryGetValue("ProcurementOrderId", out var poid) ? poid.S ?? "" : "",
            SupplierId = item.TryGetValue("SupplierId", out var sid) ? sid.S ?? "" : "",
            SupplierName = item.TryGetValue("SupplierName", out var sn) ? sn.S ?? "" : "",
            AssignedDriverId = item.TryGetValue("AssignedDriverId", out var did) ? did.S ?? "" : "",
            AssignedDriverName = item.TryGetValue("AssignedDriverName", out var dn) ? dn.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var h) ? h.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Pending" : "Pending",
            Notes = item.TryGetValue("Notes", out var notes) ? notes.S ?? "" : "",
            InvoiceS3Key = item.TryGetValue("InvoiceS3Key", out var inv) ? inv.S ?? "" : "",
            DeliveryNoteS3Key = item.TryGetValue("DeliveryNoteS3Key", out var dn) ? dn.S ?? "" : "",
            Lines = lines,
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
                ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        };
    }
}

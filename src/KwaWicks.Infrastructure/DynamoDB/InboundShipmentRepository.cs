using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class InboundShipmentRepository : IInboundShipmentRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public InboundShipmentRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"IS#{id}";
    private const string SkMeta = "META";

    public async Task<InboundShipment> CreateAsync(InboundShipment s, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(s),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return s;
    }

    public async Task<InboundShipment?> GetAsync(string id, CancellationToken ct = default)
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

    public async Task<List<InboundShipment>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "InboundShipment" }
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

        var result = new List<InboundShipment>();
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

    public async Task<InboundShipment> UpdateAsync(InboundShipment s, CancellationToken ct = default)
    {
        s.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(s)
        }, ct);
        return s;
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

    private static Dictionary<string, AttributeValue> ToItem(InboundShipment s)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = Pk(s.InboundShipmentId) },
            ["SK"] = new AttributeValue { S = SkMeta },
            ["EntityType"] = new AttributeValue { S = "InboundShipment" },
            ["InboundShipmentId"] = new AttributeValue { S = s.InboundShipmentId },
            ["SupplierId"] = new AttributeValue { S = s.SupplierId ?? "" },
            ["SupplierName"] = new AttributeValue { S = s.SupplierName ?? "" },
            ["HubId"] = new AttributeValue { S = s.HubId ?? "" },
            ["Status"] = new AttributeValue { S = s.Status ?? "Open" },
            ["TransporterName"] = new AttributeValue { S = s.TransporterName ?? "" },
            ["TransporterContact"] = new AttributeValue { S = s.TransporterContact ?? "" },
            ["VehicleReg"] = new AttributeValue { S = s.VehicleReg ?? "" },
            ["Notes"] = new AttributeValue { S = s.Notes ?? "" },
            ["CreatedByUserId"] = new AttributeValue { S = s.CreatedByUserId ?? "" },
            ["LinesJson"] = new AttributeValue { S = JsonSerializer.Serialize(s.Lines ?? new()) },
            ["CreatedAtUtc"] = new AttributeValue { S = s.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
            ["UpdatedAtUtc"] = new AttributeValue { S = s.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
        };

        if (s.ExpectedAt.HasValue)
            item["ExpectedAtUtc"] = new AttributeValue { S = s.ExpectedAt.Value.ToString("O", CultureInfo.InvariantCulture) };
        if (s.DispatchedAt.HasValue)
            item["DispatchedAtUtc"] = new AttributeValue { S = s.DispatchedAt.Value.ToString("O", CultureInfo.InvariantCulture) };
        if (s.ReceivedAt.HasValue)
            item["ReceivedAtUtc"] = new AttributeValue { S = s.ReceivedAt.Value.ToString("O", CultureInfo.InvariantCulture) };
        if (s.CompletedAt.HasValue)
            item["CompletedAtUtc"] = new AttributeValue { S = s.CompletedAt.Value.ToString("O", CultureInfo.InvariantCulture) };

        return item;
    }

    private static InboundShipment FromItem(Dictionary<string, AttributeValue> item)
    {
        var linesJson = item.TryGetValue("LinesJson", out var lj) ? lj.S : "[]";
        var lines = JsonSerializer.Deserialize<List<InboundShipmentLine>>(linesJson ?? "[]") ?? new();
        return new InboundShipment
        {
            InboundShipmentId = item.TryGetValue("InboundShipmentId", out var id) ? id.S ?? "" : "",
            SupplierId = item.TryGetValue("SupplierId", out var sid) ? sid.S ?? "" : "",
            SupplierName = item.TryGetValue("SupplierName", out var sn) ? sn.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var hub) ? hub.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Open" : "Open",
            TransporterName = item.TryGetValue("TransporterName", out var tn) ? tn.S ?? "" : "",
            TransporterContact = item.TryGetValue("TransporterContact", out var tc) ? tc.S ?? "" : "",
            VehicleReg = item.TryGetValue("VehicleReg", out var vr) ? vr.S ?? "" : "",
            Notes = item.TryGetValue("Notes", out var notes) ? notes.S ?? "" : "",
            CreatedByUserId = item.TryGetValue("CreatedByUserId", out var cbu) ? cbu.S ?? "" : "",
            Lines = lines,
            ExpectedAt = item.TryGetValue("ExpectedAtUtc", out var ea) && !string.IsNullOrEmpty(ea.S)
                ? DateTime.Parse(ea.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            DispatchedAt = item.TryGetValue("DispatchedAtUtc", out var da) && !string.IsNullOrEmpty(da.S)
                ? DateTime.Parse(da.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            ReceivedAt = item.TryGetValue("ReceivedAtUtc", out var ra) && !string.IsNullOrEmpty(ra.S)
                ? DateTime.Parse(ra.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            CompletedAt = item.TryGetValue("CompletedAtUtc", out var ca2) && !string.IsNullOrEmpty(ca2.S)
                ? DateTime.Parse(ca2.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var cat) && !string.IsNullOrEmpty(cat.S)
                ? DateTime.Parse(cat.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var uat) && !string.IsNullOrEmpty(uat.S)
                ? DateTime.Parse(uat.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        };
    }
}

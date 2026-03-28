using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class HubRequestRepository : IHubRequestRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public HubRequestRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"HUBREQUEST#{id}";
    private const string Sk = "META";

    public async Task<HubRequest> CreateAsync(HubRequest request, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(request),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return request;
    }

    public async Task<HubRequest?> GetAsync(string hubRequestId, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = Pk(hubRequestId) },
                ["SK"] = new() { S = Sk }
            }
        }, ct);
        if (resp.Item == null || resp.Item.Count == 0) return null;
        return FromItem(resp.Item);
    }

    public async Task<List<HubRequest>> ListAsync(string? status, CancellationToken ct)
    {
        var filter = "EntityType = :et";
        var vals = new Dictionary<string, AttributeValue>
        {
            [":et"] = new() { S = "HubRequest" }
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter += " AND #st = :status";
            vals[":status"] = new() { S = status };
        }

        var results = new List<HubRequest>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var req = new ScanRequest
            {
                TableName = _tableName,
                FilterExpression = filter,
                ExpressionAttributeValues = vals,
                ExclusiveStartKey = lastKey
            };

            if (!string.IsNullOrWhiteSpace(status))
            {
                req.ExpressionAttributeNames = new Dictionary<string, string> { ["#st"] = "Status" };
            }

            var resp = await _ddb.ScanAsync(req, ct);
            results.AddRange(resp.Items.Select(FromItem));
            lastKey = resp.LastEvaluatedKey?.Count > 0 ? resp.LastEvaluatedKey : null;
        } while (lastKey != null);

        return results;
    }

    public async Task<HubRequest> UpdateAsync(HubRequest request, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(request)
        }, ct);
        return request;
    }

    private static Dictionary<string, AttributeValue> ToItem(HubRequest r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = Pk(r.HubRequestId) },
            ["SK"] = new() { S = Sk },
            ["EntityType"] = new() { S = "HubRequest" },
            ["HubRequestId"] = new() { S = r.HubRequestId },
            ["RequestedBy"] = new() { S = r.RequestedBy },
            ["Message"] = new() { S = r.Message },
            ["Status"] = new() { S = r.Status },
            ["ActionedBy"] = new() { S = r.ActionedBy ?? "" },
            ["ActionNotes"] = new() { S = r.ActionNotes ?? "" },
            ["LinkedOrderId"] = new() { S = r.LinkedOrderId ?? "" },
            ["LinkedOrderType"] = new() { S = r.LinkedOrderType ?? "" },
            ["LinkedOrderRef"] = new() { S = r.LinkedOrderRef ?? "" },
            ["WhatsAppError"] = new() { S = r.WhatsAppError ?? "" },
            ["CreatedAtUtc"] = new() { S = r.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
        };

        if (r.ActionedAtUtc.HasValue)
            item["ActionedAtUtc"] = new() { S = r.ActionedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) };

        return item;
    }

    private static HubRequest FromItem(Dictionary<string, AttributeValue> i) => new()
    {
        HubRequestId = i["HubRequestId"].S,
        RequestedBy = i.TryGetValue("RequestedBy", out var rb) ? rb.S ?? "" : "",
        Message = i.TryGetValue("Message", out var msg) ? msg.S ?? "" : "",
        Status = i.TryGetValue("Status", out var st) ? st.S ?? "Pending" : "Pending",
        ActionedBy = i.TryGetValue("ActionedBy", out var ab) ? ab.S ?? "" : "",
        ActionNotes = i.TryGetValue("ActionNotes", out var an) ? an.S ?? "" : "",
        LinkedOrderId = i.TryGetValue("LinkedOrderId", out var lo) ? lo.S ?? "" : "",
        LinkedOrderType = i.TryGetValue("LinkedOrderType", out var lt) ? lt.S ?? "" : "",
        LinkedOrderRef = i.TryGetValue("LinkedOrderRef", out var lr) ? lr.S ?? "" : "",
        WhatsAppError = i.TryGetValue("WhatsAppError", out var we) ? we.S ?? "" : "",
        CreatedAtUtc = i.TryGetValue("CreatedAtUtc", out var ca)
            ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow,
        ActionedAtUtc = i.TryGetValue("ActionedAtUtc", out var aa) && !string.IsNullOrEmpty(aa.S)
            ? DateTime.Parse(aa.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null
    };
}

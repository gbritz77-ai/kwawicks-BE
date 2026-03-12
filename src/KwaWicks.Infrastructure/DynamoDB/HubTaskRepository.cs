using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class HubTaskRepository : IHubTaskRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public HubTaskRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"HUBTASK#{id}";
    private const string SkMeta = "META";

    public async Task<HubTask> CreateAsync(HubTask task, CancellationToken ct)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (string.IsNullOrWhiteSpace(task.HubTaskId))
            throw new ArgumentException("HubTaskId is required.", nameof(task));

        var req = new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(task),
            ConditionExpression = "attribute_not_exists(PK)"
        };

        await _ddb.PutItemAsync(req, ct);
        return task;
    }

    public async Task<HubTask?> GetAsync(string hubTaskId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(hubTaskId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);

        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task UpdateStatusAsync(string hubTaskId, string status, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hubTaskId)) throw new ArgumentException("hubTaskId is required.", nameof(hubTaskId));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("status is required.", nameof(status));

        var req = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(hubTaskId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            },
            UpdateExpression = "SET #s = :s",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#s"] = "Status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":s"] = new AttributeValue { S = status }
            },
            ConditionExpression = "attribute_exists(PK)"
        };

        await _ddb.UpdateItemAsync(req, ct);
    }

    private static Dictionary<string, AttributeValue> ToItem(HubTask t)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = Pk(t.HubTaskId) },
            ["SK"] = new AttributeValue { S = SkMeta },
            ["EntityType"] = new AttributeValue { S = "HubTask" },

            ["HubTaskId"] = new AttributeValue { S = t.HubTaskId },
            ["HubId"] = new AttributeValue { S = t.HubId ?? "" },
            ["Type"] = new AttributeValue { S = t.Type ?? "Invoice" },
            ["Status"] = new AttributeValue { S = t.Status ?? "Open" },

            ["InvoiceId"] = new AttributeValue { S = t.InvoiceId ?? "" },
            ["DeliveryOrderId"] = new AttributeValue { S = t.DeliveryOrderId ?? "" },

            ["Title"] = new AttributeValue { S = t.Title ?? "" },
            ["CreatedAtUtc"] = new AttributeValue { S = t.CreatedAt.ToString("O", CultureInfo.InvariantCulture) }
        };
    }

    private static HubTask FromItem(Dictionary<string, AttributeValue> item)
    {
        return new HubTask
        {
            HubTaskId = item.TryGetValue("HubTaskId", out var id) ? id.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var hub) ? hub.S ?? "" : "",
            Type = item.TryGetValue("Type", out var tp) ? tp.S ?? "Invoice" : "Invoice",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Open" : "Open",
            InvoiceId = item.TryGetValue("InvoiceId", out var inv) ? inv.S ?? "" : "",
            DeliveryOrderId = item.TryGetValue("DeliveryOrderId", out var d) ? d.S ?? "" : "",
            Title = item.TryGetValue("Title", out var title) ? title.S ?? "" : "",
            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow
        };
    }
}
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class OtpRepository : IOtpRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public OtpRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string invoiceId) => $"OTP#{invoiceId}";
    private const string SkMeta = "META";

    public async Task SaveAsync(OtpRecord record, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(record)
        }, ct);
    }

    public async Task<OtpRecord?> GetByInvoiceIdAsync(string invoiceId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(invoiceId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);

        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<List<OtpRecord>> ListByClientAsync(string clientId, CancellationToken ct)
    {
        var res = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "begins_with(PK, :p) AND SK = :sk AND ClientId = :cid",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":p"]   = new AttributeValue { S = "OTP#" },
                [":sk"]  = new AttributeValue { S = SkMeta },
                [":cid"] = new AttributeValue { S = clientId }
            }
        }, ct);

        return res.Items.Select(FromItem).OrderByDescending(r => r.SentAt).ToList();
    }

    public async Task<List<OtpRecord>> ListAllAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var res = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "begins_with(PK, :p) AND SK = :sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":p"]  = new AttributeValue { S = "OTP#" },
                [":sk"] = new AttributeValue { S = SkMeta }
            }
        }, ct);

        var records = res.Items.Select(FromItem);

        if (from.HasValue) records = records.Where(r => r.SentAt >= from.Value);
        if (to.HasValue)   records = records.Where(r => r.SentAt <= to.Value);

        return records.OrderByDescending(r => r.SentAt).ToList();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(OtpRecord r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"]            = new AttributeValue { S = Pk(r.InvoiceId) },
            ["SK"]            = new AttributeValue { S = SkMeta },
            ["EntityType"]    = new AttributeValue { S = "OtpRecord" },
            ["OtpId"]         = new AttributeValue { S = r.OtpId },
            ["InvoiceId"]     = new AttributeValue { S = r.InvoiceId },
            ["InvoiceNumber"] = new AttributeValue { S = r.InvoiceNumber },
            ["ReferenceType"] = new AttributeValue { S = r.ReferenceType },
            ["ClientId"]      = new AttributeValue { S = r.ClientId },
            ["ClientName"]    = new AttributeValue { S = r.ClientName },
            ["ClientPhone"]   = new AttributeValue { S = r.ClientPhone },
            ["InvoiceTotal"]  = new AttributeValue { N = r.InvoiceTotal.ToString(CultureInfo.InvariantCulture) },
            ["OtpCode"]       = new AttributeValue { S = r.OtpCode },
            ["Status"]        = new AttributeValue { S = r.Status },
            ["SentAt"]        = new AttributeValue { S = r.SentAt.ToString("O", CultureInfo.InvariantCulture) },
            ["ExpiresAt"]     = new AttributeValue { S = r.ExpiresAt.ToString("O", CultureInfo.InvariantCulture) },
        };

        if (r.ConfirmedAt.HasValue)
            item["ConfirmedAt"] = new AttributeValue { S = r.ConfirmedAt.Value.ToString("O", CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(r.ConfirmedByUserId))
            item["ConfirmedByUserId"] = new AttributeValue { S = r.ConfirmedByUserId };
        if (!string.IsNullOrWhiteSpace(r.BypassReason))
            item["BypassReason"] = new AttributeValue { S = r.BypassReason };

        return item;
    }

    private static OtpRecord FromItem(Dictionary<string, AttributeValue> item)
    {
        return new OtpRecord
        {
            OtpId          = item["OtpId"].S,
            InvoiceId      = item["InvoiceId"].S,
            InvoiceNumber  = item.TryGetValue("InvoiceNumber", out var inv) ? inv.S : "",
            ReferenceType  = item["ReferenceType"].S,
            ClientId       = item.TryGetValue("ClientId",   out var cid) ? cid.S : "",
            ClientName     = item.TryGetValue("ClientName", out var cn)  ? cn.S  : "",
            ClientPhone    = item.TryGetValue("ClientPhone",out var cp)  ? cp.S  : "",
            InvoiceTotal   = item.TryGetValue("InvoiceTotal", out var it) && !string.IsNullOrWhiteSpace(it.N)
                                 ? decimal.Parse(it.N, CultureInfo.InvariantCulture) : 0m,
            OtpCode        = item["OtpCode"].S,
            Status         = item["Status"].S,
            SentAt         = DateTime.Parse(item["SentAt"].S,    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ExpiresAt      = DateTime.Parse(item["ExpiresAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ConfirmedAt    = item.TryGetValue("ConfirmedAt", out var ca) ? DateTime.Parse(ca.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
            ConfirmedByUserId = item.TryGetValue("ConfirmedByUserId", out var cbu) ? cbu.S : null,
            BypassReason   = item.TryGetValue("BypassReason", out var br) ? br.S : null,
        };
    }
}

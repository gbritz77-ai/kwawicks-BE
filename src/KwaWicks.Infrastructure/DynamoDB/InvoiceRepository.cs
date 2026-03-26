using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public InvoiceRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string invoiceId) => $"INVOICE#{invoiceId}";
    private const string SkMeta = "META";
    private const string CounterPk = "COUNTER#INVOICE";
    private const string CounterSk = "SEQ";

    public async Task<string> GetNextInvoiceNumberAsync(CancellationToken ct)
    {
        var resp = await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = CounterPk },
                ["SK"] = new AttributeValue { S = CounterSk }
            },
            UpdateExpression = "ADD #val :inc",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#val"] = "Value" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":inc"] = new AttributeValue { N = "1" }
            },
            ReturnValues = ReturnValue.UPDATED_NEW
        }, ct);

        var seq = long.Parse(resp.Attributes["Value"].N);
        return $"INV{seq:D6}";
    }

    public async Task<Invoice> CreateAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        var req = new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(invoice),
            ConditionExpression = "attribute_not_exists(PK)"
        };

        await _ddb.PutItemAsync(req, ct);
        return invoice;
    }

    public async Task<Invoice?> GetAsync(string invoiceId, CancellationToken ct)
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

    public async Task<Invoice> UpdateAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        var req = new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(invoice)
        };

        await _ddb.PutItemAsync(req, ct);
        return invoice;
    }

    public async Task<List<Invoice>> ListAsync(string? hubId, string? customerId, CancellationToken ct)
    {
        var filterParts = new List<string> { "EntityType = :et" };
        var values = new Dictionary<string, AttributeValue>
        {
            [":et"] = new AttributeValue { S = "Invoice" }
        };

        if (!string.IsNullOrWhiteSpace(hubId))
        {
            filterParts.Add("HubId = :hubId");
            values[":hubId"] = new AttributeValue { S = hubId };
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            filterParts.Add("CustomerId = :customerId");
            values[":customerId"] = new AttributeValue { S = customerId };
        }

        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = string.Join(" AND ", filterParts),
            ExpressionAttributeValues = values
        };

        var result = new List<Invoice>();
        ScanResponse? response;

        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result;
    }

    private static Dictionary<string, AttributeValue> ToItem(Invoice inv) =>
        new()
        {
            ["PK"] = new AttributeValue { S = Pk(inv.InvoiceId) },
            ["SK"] = new AttributeValue { S = SkMeta },
            ["EntityType"] = new AttributeValue { S = "Invoice" },

            ["InvoiceId"] = new AttributeValue { S = inv.InvoiceId },
            ["InvoiceNumber"] = new AttributeValue { S = inv.InvoiceNumber ?? "" },
            ["CustomerId"] = new AttributeValue { S = inv.CustomerId ?? "" },
            ["HubId"] = new AttributeValue { S = inv.HubId ?? "" },
            ["DeliveryOrderId"] = new AttributeValue { S = inv.DeliveryOrderId ?? "" },
            ["CreatedByDriverId"] = new AttributeValue { S = inv.CreatedByDriverId ?? "" },
            ["Status"] = new AttributeValue { S = inv.Status ?? "Confirmed" },
            ["PaymentType"] = new AttributeValue { S = inv.PaymentType ?? "" },
            ["PaymentStatus"] = new AttributeValue { S = inv.PaymentStatus ?? "Pending" },
            ["ReceiptS3Key"] = new AttributeValue { S = inv.ReceiptS3Key ?? "" },

            ["SubTotal"] = new AttributeValue { N = inv.SubTotal.ToString(CultureInfo.InvariantCulture) },
            ["VatTotal"] = new AttributeValue { N = inv.VatTotal.ToString(CultureInfo.InvariantCulture) },
            ["GrandTotal"] = new AttributeValue { N = inv.GrandTotal.ToString(CultureInfo.InvariantCulture) },

            ["CreatedAtUtc"] = new AttributeValue { S = inv.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
            ["UpdatedAtUtc"] = new AttributeValue { S = inv.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },

            ["LinesJson"] = new AttributeValue { S = JsonSerializer.Serialize(inv.Lines ?? new List<InvoiceLine>()) }
        };

    private static Invoice FromItem(Dictionary<string, AttributeValue> item)
    {
        var linesJson = item.TryGetValue("LinesJson", out var lj) ? lj.S : "[]";
        var lines = JsonSerializer.Deserialize<List<InvoiceLine>>(linesJson ?? "[]") ?? new();

        return new Invoice
        {
            InvoiceId = item.TryGetValue("InvoiceId", out var id) ? id.S ?? "" : "",
            InvoiceNumber = item.TryGetValue("InvoiceNumber", out var invNum) ? invNum.S ?? "" : "",
            CustomerId = item.TryGetValue("CustomerId", out var c) ? c.S ?? "" : "",
            HubId = item.TryGetValue("HubId", out var h) ? h.S ?? "" : "",
            DeliveryOrderId = item.TryGetValue("DeliveryOrderId", out var doi) ? doi.S ?? "" : "",
            CreatedByDriverId = item.TryGetValue("CreatedByDriverId", out var did) ? did.S ?? "" : "",
            Status = item.TryGetValue("Status", out var st) ? st.S ?? "Confirmed" : "Confirmed",
            PaymentType = item.TryGetValue("PaymentType", out var pt) ? pt.S ?? "" : "",
            PaymentStatus = item.TryGetValue("PaymentStatus", out var ps) ? ps.S ?? "Pending" : "Pending",
            ReceiptS3Key = item.TryGetValue("ReceiptS3Key", out var rk) ? rk.S ?? "" : "",

            SubTotal = item.TryGetValue("SubTotal", out var sub) ? decimal.Parse(sub.N, CultureInfo.InvariantCulture) : 0m,
            VatTotal = item.TryGetValue("VatTotal", out var vat) ? decimal.Parse(vat.N, CultureInfo.InvariantCulture) : 0m,
            GrandTotal = item.TryGetValue("GrandTotal", out var gt) ? decimal.Parse(gt.N, CultureInfo.InvariantCulture) : 0m,

            CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
                ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow,

            UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
                ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : DateTime.UtcNow,

            Lines = lines
        };
    }
}

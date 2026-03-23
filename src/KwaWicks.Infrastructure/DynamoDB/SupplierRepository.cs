using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class SupplierRepository : ISupplierRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public SupplierRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"SUPPLIER#{id}";
    private const string SkMeta = "META";

    public async Task<Supplier> CreateAsync(Supplier supplier, CancellationToken ct = default)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(supplier),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return supplier;
    }

    public async Task<Supplier?> GetAsync(string supplierId, CancellationToken ct = default)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(supplierId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);
        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<List<Supplier>> ListAsync(CancellationToken ct = default)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new AttributeValue { S = "Supplier" }
            }
        };

        var result = new List<Supplier>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderBy(s => s.Name).ToList();
    }

    public async Task<Supplier> UpdateAsync(Supplier supplier, CancellationToken ct = default)
    {
        supplier.UpdatedAt = DateTime.UtcNow;
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(supplier)
        }, ct);
        return supplier;
    }

    public async Task DeleteAsync(string supplierId, CancellationToken ct = default)
    {
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(supplierId) },
                ["SK"] = new AttributeValue { S = SkMeta }
            }
        }, ct);
    }

    private static Dictionary<string, AttributeValue> ToItem(Supplier s) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(s.SupplierId) },
        ["SK"] = new AttributeValue { S = SkMeta },
        ["EntityType"] = new AttributeValue { S = "Supplier" },
        ["SupplierId"] = new AttributeValue { S = s.SupplierId },
        ["Name"] = new AttributeValue { S = s.Name ?? "" },
        ["AddressJson"] = new AttributeValue { S = JsonSerializer.Serialize(s.Address) },
        ["ContactPersonJson"] = new AttributeValue { S = JsonSerializer.Serialize(s.ContactPerson) },
        ["ContactFinanceJson"] = new AttributeValue { S = JsonSerializer.Serialize(s.ContactFinance) },
        ["BankDetailsJson"] = new AttributeValue { S = JsonSerializer.Serialize(s.BankDetails) },
        ["CreatedAtUtc"] = new AttributeValue { S = s.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = s.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) },
    };

    private static Supplier FromItem(Dictionary<string, AttributeValue> item) => new()
    {
        SupplierId = item.TryGetValue("SupplierId", out var id) ? id.S ?? "" : "",
        Name = item.TryGetValue("Name", out var n) ? n.S ?? "" : "",
        Address = item.TryGetValue("AddressJson", out var aj)
            ? JsonSerializer.Deserialize<SupplierAddress>(aj.S ?? "{}") ?? new() : new(),
        ContactPerson = item.TryGetValue("ContactPersonJson", out var cpj)
            ? JsonSerializer.Deserialize<SupplierContact>(cpj.S ?? "{}") ?? new() : new(),
        ContactFinance = item.TryGetValue("ContactFinanceJson", out var cfj)
            ? JsonSerializer.Deserialize<SupplierContactFinance>(cfj.S ?? "{}") ?? new() : new(),
        BankDetails = item.TryGetValue("BankDetailsJson", out var bdj)
            ? JsonSerializer.Deserialize<SupplierBankDetails>(bdj.S ?? "{}") ?? new() : new(),
        CreatedAt = item.TryGetValue("CreatedAtUtc", out var ca)
            ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
        UpdatedAt = item.TryGetValue("UpdatedAtUtc", out var ua)
            ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
    };
}

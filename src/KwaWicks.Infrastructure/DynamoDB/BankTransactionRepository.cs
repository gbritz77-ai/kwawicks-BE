using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class BankTransactionRepository : IBankTransactionRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public BankTransactionRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"BANKTX#{id}";
    private const string SkMeta = "META";
    private const string PkConfig = "BANKTX_CONFIG";

    // ── Single get ─────────────────────────────────────────────────────────

    public async Task<BankTransaction?> GetAsync(string transactionId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = Pk(transactionId) },
                ["SK"] = new() { S = SkMeta }
            }
        }, ct);

        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    // ── List all ───────────────────────────────────────────────────────────

    public async Task<List<BankTransaction>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new() { S = "BankTransaction" }
            }
        };

        var result = new List<BankTransaction>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItem));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(t => t.Date).ToList();
    }

    // ── Dedup keys (lightweight scan for import dedup) ─────────────────────

    public async Task<HashSet<string>> GetDedupKeysAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new() { S = "BankTransaction" }
            },
            ProjectionExpression = "DedupKey"
        };

        var keys = new HashSet<string>(StringComparer.Ordinal);
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            foreach (var item in response.Items)
                if (item.TryGetValue("DedupKey", out var v) && !string.IsNullOrEmpty(v.S))
                    keys.Add(v.S);
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return keys;
    }

    // ── Create ─────────────────────────────────────────────────────────────

    public async Task CreateAsync(BankTransaction tx, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(tx)
        }, ct);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    public async Task UpdateAsync(BankTransaction tx, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(tx)
        }, ct);
    }

    // ── Batch create ───────────────────────────────────────────────────────

    public async Task BatchCreateAsync(IEnumerable<BankTransaction> transactions, CancellationToken ct)
    {
        var allTx = transactions.ToList();
        if (allTx.Count == 0) return;

        // DynamoDB BatchWriteItem limit: 25 items per call
        for (int i = 0; i < allTx.Count; i += 25)
        {
            var batch = allTx.Skip(i).Take(25).ToList();
            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_tableName] = batch.Select(tx => new WriteRequest
                    {
                        PutRequest = new PutRequest { Item = ToItem(tx) }
                    }).ToList()
                }
            };

            BatchWriteItemResponse? response;
            do
            {
                response = await _ddb.BatchWriteItemAsync(request, ct);
                // Retry unprocessed items
                if (response.UnprocessedItems.Count > 0)
                    request = new BatchWriteItemRequest { RequestItems = response.UnprocessedItems };
            }
            while (response.UnprocessedItems.Count > 0);
        }
    }

    // ── Import config (tracks import count + last imported at) ────────────

    public async Task<(int ImportCount, DateTime LastImportedAt)> GetImportConfigAsync(CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = PkConfig },
                ["SK"] = new() { S = SkMeta }
            }
        }, ct);

        if (res.Item is null || res.Item.Count == 0)
            return (0, DateTime.MinValue);

        var count = res.Item.TryGetValue("ImportCount", out var ic) ? int.Parse(ic.N) : 0;
        var last = res.Item.TryGetValue("LastImportedAt", out var lia)
            ? DateTime.Parse(lia.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.MinValue;

        return (count, last);
    }

    public async Task SaveImportConfigAsync(int importCount, DateTime lastImportedAt, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"]             = new() { S = PkConfig },
                ["SK"]             = new() { S = SkMeta },
                ["EntityType"]     = new() { S = "BankTxConfig" },
                ["ImportCount"]    = new() { N = importCount.ToString() },
                ["LastImportedAt"] = new() { S = lastImportedAt.ToString("O", CultureInfo.InvariantCulture) }
            }
        }, ct);
    }

    // ── Serialisation ──────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(BankTransaction tx)
    {
        var dedupKey = $"{tx.Date.Date:yyyy-MM-dd}|{tx.Type}|{Math.Round(tx.Amount, 2):F2}|{tx.Description.Trim().ToUpperInvariant()}";
        return new Dictionary<string, AttributeValue>
        {
            ["PK"]            = new() { S = Pk(tx.TransactionId) },
            ["SK"]            = new() { S = SkMeta },
            ["EntityType"]    = new() { S = "BankTransaction" },
            ["TransactionId"] = new() { S = tx.TransactionId },
            ["DedupKey"]      = new() { S = dedupKey },
            ["DateStr"]       = new() { S = tx.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            ["Type"]          = new() { S = tx.Type },
            ["IsAllocated"]   = new() { BOOL = tx.IsAllocated },
            ["TxJson"]        = new() { S = JsonSerializer.Serialize(tx) }
        };
    }

    private static BankTransaction FromItem(Dictionary<string, AttributeValue> item)
    {
        var txJson = item.TryGetValue("TxJson", out var j) ? j.S : "{}";
        return JsonSerializer.Deserialize<BankTransaction>(txJson ?? "{}") ?? new BankTransaction();
    }
}

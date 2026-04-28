using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class BankStatementRepository : IBankStatementRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public BankStatementRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"BANKSTMT#{id}";
    private const string SkMeta = "META";

    public async Task<BankStatement> CreateAsync(BankStatement statement, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(statement),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return statement;
    }

    public async Task<BankStatement?> GetAsync(string statementId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = Pk(statementId) },
                ["SK"] = new() { S = SkMeta }
            }
        }, ct);

        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<BankStatement> UpdateAsync(BankStatement statement, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(statement)
        }, ct);
        return statement;
    }

    public async Task<List<BankStatement>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new() { S = "BankStatement" }
            },
            // Don't scan transaction JSON on list — just metadata
            ProjectionExpression = "StatementId, FileName, S3Key, TransactionCount, CreditCount, TotalCredits, UploadedAtUtc, AllocatedCount"
        };

        var result = new List<BankStatement>();
        ScanResponse? response;
        do
        {
            response = await _ddb.ScanAsync(req, ct);
            result.AddRange(response.Items.Select(FromItemSummary));
            req.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey is { Count: > 0 });

        return result.OrderByDescending(s => s.UploadedAt).ToList();
    }

    // ── Serialisation ──────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(BankStatement s)
    {
        var allocated = s.Transactions.Count(t => t.IsAllocated);
        return new Dictionary<string, AttributeValue>
        {
            ["PK"]             = new() { S = Pk(s.StatementId) },
            ["SK"]             = new() { S = SkMeta },
            ["EntityType"]     = new() { S = "BankStatement" },
            ["StatementId"]    = new() { S = s.StatementId },
            ["FileName"]       = new() { S = s.FileName },
            ["S3Key"]          = new() { S = s.S3Key },
            ["TransactionCount"] = new() { N = s.TransactionCount.ToString() },
            ["CreditCount"]    = new() { N = s.CreditCount.ToString() },
            ["AllocatedCount"] = new() { N = allocated.ToString() },
            ["TotalCredits"]   = new() { N = s.TotalCredits.ToString(CultureInfo.InvariantCulture) },
            ["UploadedAtUtc"]  = new() { S = s.UploadedAt.ToString("O", CultureInfo.InvariantCulture) },
            ["TransactionsJson"] = new() { S = JsonSerializer.Serialize(s.Transactions) }
        };
    }

    private static BankStatement FromItem(Dictionary<string, AttributeValue> item)
    {
        var txJson = item.TryGetValue("TransactionsJson", out var txj) ? txj.S : "[]";
        var transactions = JsonSerializer.Deserialize<List<BankTransactionRecord>>(txJson ?? "[]") ?? new();

        return new BankStatement
        {
            StatementId      = item.TryGetValue("StatementId",     out var id)  ? id.S  ?? "" : "",
            FileName         = item.TryGetValue("FileName",         out var fn)  ? fn.S  ?? "" : "",
            S3Key            = item.TryGetValue("S3Key",            out var sk)  ? sk.S  ?? "" : "",
            TransactionCount = item.TryGetValue("TransactionCount", out var tc)  ? int.Parse(tc.N) : 0,
            CreditCount      = item.TryGetValue("CreditCount",      out var cc)  ? int.Parse(cc.N) : 0,
            TotalCredits     = item.TryGetValue("TotalCredits",     out var tot) ? decimal.Parse(tot.N, CultureInfo.InvariantCulture) : 0m,
            UploadedAt       = item.TryGetValue("UploadedAtUtc",    out var ua)
                                 ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                                 : DateTime.UtcNow,
            Transactions = transactions.Select(t => new BankTransaction
            {
                TransactionId         = t.TransactionId,
                Date                  = t.Date,
                Description           = t.Description,
                Reference             = t.Reference,
                Amount                = t.Amount,
                Type                  = t.Type,
                IsAllocated           = t.IsAllocated,
                AllocatedInvoiceId    = t.AllocatedInvoiceId,
                AllocatedInvoiceNumber = t.AllocatedInvoiceNumber,
                AllocatedAt           = t.AllocatedAt
            }).ToList()
        };
    }

    private static BankStatement FromItemSummary(Dictionary<string, AttributeValue> item) =>
        new BankStatement
        {
            StatementId      = item.TryGetValue("StatementId",     out var id)  ? id.S  ?? "" : "",
            FileName         = item.TryGetValue("FileName",         out var fn)  ? fn.S  ?? "" : "",
            S3Key            = item.TryGetValue("S3Key",            out var sk)  ? sk.S  ?? "" : "",
            TransactionCount = item.TryGetValue("TransactionCount", out var tc)  ? int.Parse(tc.N) : 0,
            CreditCount      = item.TryGetValue("CreditCount",      out var cc)  ? int.Parse(cc.N) : 0,
            AllocatedCount   = item.TryGetValue("AllocatedCount",   out var ac)  ? int.Parse(ac.N) : 0,
            TotalCredits     = item.TryGetValue("TotalCredits",     out var tot) ? decimal.Parse(tot.N, CultureInfo.InvariantCulture) : 0m,
            UploadedAt       = item.TryGetValue("UploadedAtUtc",    out var ua)
                                 ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                                 : DateTime.UtcNow,
            Transactions = new List<BankTransaction>() // not loaded on list
        };

    // Internal record matching the JSON shape stored in TransactionsJson
    private record BankTransactionRecord(
        string TransactionId, DateTime Date, string Description, string Reference,
        decimal Amount, string Type, bool IsAllocated, string AllocatedInvoiceId,
        string AllocatedInvoiceNumber, DateTime? AllocatedAt);
}

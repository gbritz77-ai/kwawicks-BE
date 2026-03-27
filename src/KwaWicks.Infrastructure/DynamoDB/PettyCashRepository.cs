using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class PettyCashRepository : IPettyCashRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public PettyCashRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string EntryPk(string id) => $"PETTYCASH#{id}";
    private static string CashupPk(string id) => $"CASHUP#{id}";
    private const string SkMeta = "META";

    // ── Entries ────────────────────────────────────────────────────────────

    public async Task<PettyCashEntry> CreateEntryAsync(PettyCashEntry entry, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = EntryToItem(entry),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return entry;
    }

    public async Task<List<PettyCashEntry>> ListEntriesAsync(string? from, string? to, CancellationToken ct)
    {
        var filter = "EntityType = :et";
        var vals = new Dictionary<string, AttributeValue> { [":et"] = new() { S = "PettyCashEntry" } };

        if (!string.IsNullOrWhiteSpace(from))
        {
            filter += " AND EntryDate >= :from";
            vals[":from"] = new() { S = from };
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            filter += " AND EntryDate <= :to";
            vals[":to"] = new() { S = to };
        }

        return await ScanEntries(filter, vals, ct);
    }

    public async Task<List<PettyCashEntry>> ListOpenEntriesAsync(CancellationToken ct)
    {
        return await ScanEntries(
            "EntityType = :et AND (attribute_not_exists(CashupId) OR CashupId = :empty)",
            new Dictionary<string, AttributeValue>
            {
                [":et"] = new() { S = "PettyCashEntry" },
                [":empty"] = new() { S = "" }
            }, ct);
    }

    public async Task MarkEntriesCashedUpAsync(IEnumerable<string> entryIds, string cashupId, CancellationToken ct)
    {
        foreach (var id in entryIds)
        {
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = EntryPk(id) },
                    ["SK"] = new() { S = SkMeta }
                },
                UpdateExpression = "SET CashupId = :cid",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":cid"] = new() { S = cashupId }
                }
            }, ct);
        }
    }

    private async Task<List<PettyCashEntry>> ScanEntries(
        string filter, Dictionary<string, AttributeValue> vals, CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = filter,
            ExpressionAttributeValues = vals
        };
        var result = new List<PettyCashEntry>();
        ScanResponse? resp;
        do
        {
            resp = await _ddb.ScanAsync(req, ct);
            result.AddRange(resp.Items.Select(EntryFromItem));
            req.ExclusiveStartKey = resp.LastEvaluatedKey;
        } while (resp.LastEvaluatedKey is { Count: > 0 });
        return result.OrderBy(e => e.EntryDate).ThenBy(e => e.CreatedAtUtc).ToList();
    }

    // ── Cashups ────────────────────────────────────────────────────────────

    public async Task<PettyCashup> CreateCashupAsync(PettyCashup cashup, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = CashupToItem(cashup),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return cashup;
    }

    public async Task<PettyCashup?> GetLatestCashupAsync(CancellationToken ct)
    {
        var all = await ListCashupsAsync(ct);
        return all.MaxBy(c => c.CashupDate);
    }

    public async Task<List<PettyCashup>> ListCashupsAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new() { S = "PettyCashup" }
            }
        };
        var result = new List<PettyCashup>();
        ScanResponse? resp;
        do
        {
            resp = await _ddb.ScanAsync(req, ct);
            result.AddRange(resp.Items.Select(CashupFromItem));
            req.ExclusiveStartKey = resp.LastEvaluatedKey;
        } while (resp.LastEvaluatedKey is { Count: > 0 });
        return result.OrderByDescending(c => c.CashupDate).ToList();
    }

    // ── Serialisation ──────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> EntryToItem(PettyCashEntry e) => new()
    {
        ["PK"] = new() { S = EntryPk(e.EntryId) },
        ["SK"] = new() { S = SkMeta },
        ["EntityType"] = new() { S = "PettyCashEntry" },
        ["EntryId"] = new() { S = e.EntryId },
        ["Type"] = new() { S = e.Type },
        ["Amount"] = new() { N = e.Amount.ToString(CultureInfo.InvariantCulture) },
        ["Description"] = new() { S = e.Description ?? "" },
        ["Category"] = new() { S = e.Category ?? "Other" },
        ["RecipientName"] = new() { S = e.RecipientName ?? "" },
        ["RecordedBy"] = new() { S = e.RecordedBy ?? "" },
        ["EntryDate"] = new() { S = e.EntryDate ?? "" },
        ["CashupId"] = new() { S = e.CashupId ?? "" },
        ["CreatedAtUtc"] = new() { S = e.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
    };

    private static PettyCashEntry EntryFromItem(Dictionary<string, AttributeValue> i) => new()
    {
        EntryId = i.TryGetValue("EntryId", out var id) ? id.S ?? "" : "",
        Type = i.TryGetValue("Type", out var t) ? t.S ?? "Out" : "Out",
        Amount = i.TryGetValue("Amount", out var a) ? decimal.Parse(a.N, CultureInfo.InvariantCulture) : 0m,
        Description = i.TryGetValue("Description", out var d) ? d.S ?? "" : "",
        Category = i.TryGetValue("Category", out var cat) ? cat.S ?? "Other" : "Other",
        RecipientName = i.TryGetValue("RecipientName", out var rn) ? rn.S ?? "" : "",
        RecordedBy = i.TryGetValue("RecordedBy", out var rb) ? rb.S ?? "" : "",
        EntryDate = i.TryGetValue("EntryDate", out var ed) ? ed.S ?? "" : "",
        CashupId = i.TryGetValue("CashupId", out var ci) ? ci.S ?? "" : "",
        CreatedAtUtc = i.TryGetValue("CreatedAtUtc", out var ca)
            ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow
    };

    private static Dictionary<string, AttributeValue> CashupToItem(PettyCashup c) => new()
    {
        ["PK"] = new() { S = CashupPk(c.CashupId) },
        ["SK"] = new() { S = SkMeta },
        ["EntityType"] = new() { S = "PettyCashup" },
        ["CashupId"] = new() { S = c.CashupId },
        ["CashupDate"] = new() { S = c.CashupDate ?? "" },
        ["OpeningBalance"] = new() { N = c.OpeningBalance.ToString(CultureInfo.InvariantCulture) },
        ["TotalIn"] = new() { N = c.TotalIn.ToString(CultureInfo.InvariantCulture) },
        ["TotalOut"] = new() { N = c.TotalOut.ToString(CultureInfo.InvariantCulture) },
        ["ExpectedBalance"] = new() { N = c.ExpectedBalance.ToString(CultureInfo.InvariantCulture) },
        ["ActualBalance"] = new() { N = c.ActualBalance.ToString(CultureInfo.InvariantCulture) },
        ["Variance"] = new() { N = c.Variance.ToString(CultureInfo.InvariantCulture) },
        ["Notes"] = new() { S = c.Notes ?? "" },
        ["ClosedBy"] = new() { S = c.ClosedBy ?? "" },
        ["CreatedAtUtc"] = new() { S = c.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
    };

    private static PettyCashup CashupFromItem(Dictionary<string, AttributeValue> i) => new()
    {
        CashupId = i.TryGetValue("CashupId", out var id) ? id.S ?? "" : "",
        CashupDate = i.TryGetValue("CashupDate", out var cd) ? cd.S ?? "" : "",
        OpeningBalance = i.TryGetValue("OpeningBalance", out var ob) ? decimal.Parse(ob.N, CultureInfo.InvariantCulture) : 0m,
        TotalIn = i.TryGetValue("TotalIn", out var ti) ? decimal.Parse(ti.N, CultureInfo.InvariantCulture) : 0m,
        TotalOut = i.TryGetValue("TotalOut", out var to2) ? decimal.Parse(to2.N, CultureInfo.InvariantCulture) : 0m,
        ExpectedBalance = i.TryGetValue("ExpectedBalance", out var eb) ? decimal.Parse(eb.N, CultureInfo.InvariantCulture) : 0m,
        ActualBalance = i.TryGetValue("ActualBalance", out var ab) ? decimal.Parse(ab.N, CultureInfo.InvariantCulture) : 0m,
        Variance = i.TryGetValue("Variance", out var v) ? decimal.Parse(v.N, CultureInfo.InvariantCulture) : 0m,
        Notes = i.TryGetValue("Notes", out var n) ? n.S ?? "" : "",
        ClosedBy = i.TryGetValue("ClosedBy", out var cb) ? cb.S ?? "" : "",
        CreatedAtUtc = i.TryGetValue("CreatedAtUtc", out var ca)
            ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow
    };
}

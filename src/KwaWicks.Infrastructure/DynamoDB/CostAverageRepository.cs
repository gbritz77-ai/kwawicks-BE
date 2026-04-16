using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace KwaWicks.Infrastructure.DynamoDB;

public class CostAverageRepository : ICostAverageRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public CostAverageRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string speciesId) => $"COSTAVG#{speciesId}";
    private static string Sk(string month)      => $"MONTH#{month}";

    public async Task UpsertAsync(CostAverageRecord record, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(record)
        }, ct);
    }

    public async Task<List<CostAverageRecord>> ListBySpeciesAsync(string speciesId, CancellationToken ct)
    {
        var res = await _ddb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = Pk(speciesId) },
                [":sk"] = new AttributeValue { S = "MONTH#" }
            }
        }, ct);

        return res.Items.Select(FromItem).OrderBy(r => r.Month).ToList();
    }

    public async Task<List<CostAverageRecord>> ListByMonthAsync(string month, CancellationToken ct)
    {
        var res = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "begins_with(PK, :p) AND SK = :sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":p"]  = new AttributeValue { S = "COSTAVG#" },
                [":sk"] = new AttributeValue { S = Sk(month) }
            }
        }, ct);

        return res.Items.Select(FromItem).OrderBy(r => r.SpeciesName).ToList();
    }

    public async Task<List<CostAverageRecord>> ListAllAsync(CancellationToken ct)
    {
        var res = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "begins_with(PK, :p)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":p"] = new AttributeValue { S = "COSTAVG#" }
            }
        }, ct);

        return res.Items
            .Select(FromItem)
            .OrderBy(r => r.SpeciesName)
            .ThenBy(r => r.Month)
            .ToList();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> ToItem(CostAverageRecord r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"]               = new AttributeValue { S = Pk(r.SpeciesId) },
            ["SK"]               = new AttributeValue { S = Sk(r.Month) },
            ["EntityType"]       = new AttributeValue { S = "CostAverageRecord" },
            ["SpeciesId"]        = new AttributeValue { S = r.SpeciesId },
            ["SpeciesName"]      = new AttributeValue { S = r.SpeciesName },
            ["Month"]            = new AttributeValue { S = r.Month },
            ["TotalQty"]         = new AttributeValue { N = r.TotalQty.ToString(CultureInfo.InvariantCulture) },
            ["AvgCostExVat"]     = new AttributeValue { N = r.AvgCostExVat.ToString(CultureInfo.InvariantCulture) },
            ["AvgCostIncVat"]    = new AttributeValue { N = r.AvgCostIncVat.ToString(CultureInfo.InvariantCulture) },
            ["VatRate"]          = new AttributeValue { N = r.VatRate.ToString(CultureInfo.InvariantCulture) },
            ["PriorUnitCost"]    = new AttributeValue { N = r.PriorUnitCost.ToString(CultureInfo.InvariantCulture) },
            ["AppliedToSpecies"] = new AttributeValue { BOOL = r.AppliedToSpecies },
            ["CalculatedAt"]     = new AttributeValue { S = r.CalculatedAt.ToString("O", CultureInfo.InvariantCulture) },
            ["SourcesJson"]      = new AttributeValue { S = JsonSerializer.Serialize(r.Sources) },
        };
        return item;
    }

    private static CostAverageRecord FromItem(Dictionary<string, AttributeValue> item)
    {
        List<CostContributionLine> sources = new();
        if (item.TryGetValue("SourcesJson", out var sj) && !string.IsNullOrWhiteSpace(sj.S))
            sources = JsonSerializer.Deserialize<List<CostContributionLine>>(sj.S) ?? new();

        return new CostAverageRecord
        {
            SpeciesId       = item["SpeciesId"].S,
            SpeciesName     = item["SpeciesName"].S,
            Month           = item["Month"].S,
            TotalQty        = int.Parse(item["TotalQty"].N, CultureInfo.InvariantCulture),
            AvgCostExVat    = decimal.Parse(item["AvgCostExVat"].N, CultureInfo.InvariantCulture),
            AvgCostIncVat   = decimal.Parse(item["AvgCostIncVat"].N, CultureInfo.InvariantCulture),
            VatRate         = decimal.Parse(item["VatRate"].N, CultureInfo.InvariantCulture),
            PriorUnitCost   = decimal.Parse(item["PriorUnitCost"].N, CultureInfo.InvariantCulture),
            AppliedToSpecies= item.TryGetValue("AppliedToSpecies", out var a) && a.BOOL == true,
            CalculatedAt    = DateTime.Parse(item["CalculatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Sources         = sources,
        };
    }
}

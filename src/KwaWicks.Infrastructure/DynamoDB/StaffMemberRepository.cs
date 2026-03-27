using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using System.Globalization;

namespace KwaWicks.Infrastructure.DynamoDB;

public class StaffMemberRepository : IStaffMemberRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public StaffMemberRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string id) => $"STAFF#{id}";
    private const string SkProfile = "PROFILE";

    public async Task<StaffMember> CreateAsync(StaffMember member, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(member),
            ConditionExpression = "attribute_not_exists(PK)"
        }, ct);
        return member;
    }

    public async Task<StaffMember?> GetAsync(string staffMemberId, CancellationToken ct)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(staffMemberId) },
                ["SK"] = new AttributeValue { S = SkProfile }
            }
        }, ct);
        return res.Item is null || res.Item.Count == 0 ? null : FromItem(res.Item);
    }

    public async Task<StaffMember> UpdateAsync(StaffMember member, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(member)
        }, ct);
        return member;
    }

    public async Task<List<StaffMember>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new AttributeValue { S = "StaffMember" }
            }
        };

        var result = new List<StaffMember>();
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

    private static Dictionary<string, AttributeValue> ToItem(StaffMember m) => new()
    {
        ["PK"] = new AttributeValue { S = Pk(m.StaffMemberId) },
        ["SK"] = new AttributeValue { S = SkProfile },
        ["EntityType"] = new AttributeValue { S = "StaffMember" },
        ["StaffMemberId"] = new AttributeValue { S = m.StaffMemberId },
        ["Name"] = new AttributeValue { S = m.Name ?? "" },
        ["Phone"] = new AttributeValue { S = m.Phone ?? "" },
        ["Department"] = new AttributeValue { S = m.Department ?? "" },
        ["IsActive"] = new AttributeValue { BOOL = m.IsActive },
        ["CreatedAtUtc"] = new AttributeValue { S = m.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) },
        ["UpdatedAtUtc"] = new AttributeValue { S = m.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
    };

    private static StaffMember FromItem(Dictionary<string, AttributeValue> item) => new()
    {
        StaffMemberId = item.TryGetValue("StaffMemberId", out var id) ? id.S ?? "" : "",
        Name = item.TryGetValue("Name", out var n) ? n.S ?? "" : "",
        Phone = item.TryGetValue("Phone", out var ph) ? ph.S ?? "" : "",
        Department = item.TryGetValue("Department", out var d) ? d.S ?? "" : "",
        IsActive = item.TryGetValue("IsActive", out var ia) && ia.IsBOOLSet && ia.BOOL == true,
        CreatedAtUtc = item.TryGetValue("CreatedAtUtc", out var ca)
            ? DateTime.Parse(ca.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow,
        UpdatedAtUtc = item.TryGetValue("UpdatedAtUtc", out var ua)
            ? DateTime.Parse(ua.S!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow
    };
}

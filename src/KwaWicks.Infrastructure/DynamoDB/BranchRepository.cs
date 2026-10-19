using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Infrastructure.DynamoDB;

public class BranchRepository : IBranchRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public BranchRepository(IAmazonDynamoDB ddb, string table)
    {
        _ddb   = ddb;
        _table = table;
    }

    // ── Branches ──────────────────────────────────────────────────────────────

    public async Task<List<Branch>> ListAsync(CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _table,
            FilterExpression = "EntityType = :et",
            ExpressionAttributeValues = new() { [":et"] = new() { S = "Branch" } }
        };
        var result = new List<Branch>();
        ScanResponse? resp;
        do
        {
            resp = await _ddb.ScanAsync(req, ct);
            result.AddRange(resp.Items.Select(MapBranch));
            req.ExclusiveStartKey = resp.LastEvaluatedKey;
        } while (resp.LastEvaluatedKey is { Count: > 0 });
        return result;
    }

    public async Task<Branch?> GetAsync(string branchId, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new() { ["PK"] = new() { S = $"BRANCH#{branchId}" }, ["SK"] = new() { S = "META" } }
        }, ct);
        return resp.Item?.Count > 0 ? MapBranch(resp.Item) : null;
    }

    public async Task CreateAsync(Branch branch, CancellationToken ct)
        => await _ddb.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToBranchItem(branch) }, ct);

    public async Task UpdateAsync(Branch branch, CancellationToken ct)
        => await _ddb.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToBranchItem(branch) }, ct);

    public async Task DeleteAsync(string branchId, CancellationToken ct)
        => await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = new() { ["PK"] = new() { S = $"BRANCH#{branchId}" }, ["SK"] = new() { S = "META" } }
        }, ct);

    // ── Branch Users ──────────────────────────────────────────────────────────

    public async Task<List<BranchUser>> ListUsersAsync(string branchId, CancellationToken ct)
    {
        var resp = await _ddb.QueryAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = $"BRANCH#{branchId}" },
                [":sk"] = new() { S = "USER#" }
            }
        }, ct);
        return resp.Items.Select(MapUser).ToList();
    }

    public async Task<BranchUser?> GetUserByUsernameAsync(string username, CancellationToken ct)
    {
        var req = new ScanRequest
        {
            TableName = _table,
            FilterExpression = "EntityType = :et AND #un = :un",
            ExpressionAttributeNames  = new() { ["#un"] = "Username" },
            ExpressionAttributeValues = new()
            {
                [":et"] = new() { S = "BranchUser" },
                [":un"] = new() { S = username }
            }
        };
        var resp = await _ddb.ScanAsync(req, ct);
        return resp.Items.Count > 0 ? MapUser(resp.Items[0]) : null;
    }

    public async Task CreateUserAsync(BranchUser user, CancellationToken ct)
        => await _ddb.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToUserItem(user) }, ct);

    public async Task UpdateUserAsync(BranchUser user, CancellationToken ct)
        => await _ddb.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToUserItem(user) }, ct);

    public async Task DeleteUserAsync(string branchId, string branchUserId, CancellationToken ct)
        => await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = new()
            {
                ["PK"] = new() { S = $"BRANCH#{branchId}" },
                ["SK"] = new() { S = $"USER#{branchUserId}" }
            }
        }, ct);

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static Branch MapBranch(Dictionary<string, AttributeValue> i) => new()
    {
        BranchId    = i.TryGetValue("BranchId",    out var bi) ? bi.S : "",
        Name        = i.TryGetValue("Name",         out var n)  ? n.S  : "",
        Location    = i.TryGetValue("Location",     out var l)  ? l.S  : "",
        IsActive    = !i.TryGetValue("IsActive",    out var ia) || ia.BOOL == true,
        Permissions = i.TryGetValue("Permissions",  out var p)  && p.L != null
                        ? p.L.Select(x => x.S).ToList() : new(),
        CreatedAt   = i.TryGetValue("CreatedAt",    out var ca)
                        ? DateTime.Parse(ca.S) : DateTime.UtcNow,
    };

    private static BranchUser MapUser(Dictionary<string, AttributeValue> i) => new()
    {
        BranchUserId = i.TryGetValue("BranchUserId", out var bui) ? bui.S : "",
        BranchId     = i.TryGetValue("BranchId",     out var bi)  ? bi.S  : "",
        Username     = i.TryGetValue("Username",      out var u)   ? u.S   : "",
        PinHash      = i.TryGetValue("PinHash",       out var ph)  ? ph.S  : "",
        IsActive     = !i.TryGetValue("IsActive",     out var ia)  || ia.BOOL == true,
        CreatedAt    = i.TryGetValue("CreatedAt",     out var ca)
                         ? DateTime.Parse(ca.S) : DateTime.UtcNow,
    };

    private static Dictionary<string, AttributeValue> ToBranchItem(Branch b) => new()
    {
        ["PK"]          = new() { S = $"BRANCH#{b.BranchId}" },
        ["SK"]          = new() { S = "META" },
        ["EntityType"]  = new() { S = "Branch" },
        ["BranchId"]    = new() { S = b.BranchId },
        ["Name"]        = new() { S = b.Name },
        ["Location"]    = new() { S = b.Location },
        ["IsActive"]    = new() { BOOL = b.IsActive },
        ["Permissions"] = new() { L = b.Permissions.Select(x => new AttributeValue { S = x }).ToList() },
        ["CreatedAt"]   = new() { S = b.CreatedAt.ToString("O") },
    };

    private static Dictionary<string, AttributeValue> ToUserItem(BranchUser u) => new()
    {
        ["PK"]           = new() { S = $"BRANCH#{u.BranchId}" },
        ["SK"]           = new() { S = $"USER#{u.BranchUserId}" },
        ["EntityType"]   = new() { S = "BranchUser" },
        ["BranchUserId"] = new() { S = u.BranchUserId },
        ["BranchId"]     = new() { S = u.BranchId },
        ["Username"]     = new() { S = u.Username },
        ["PinHash"]      = new() { S = u.PinHash },
        ["IsActive"]     = new() { BOOL = u.IsActive },
        ["CreatedAt"]    = new() { S = u.CreatedAt.ToString("O") },
    };
}

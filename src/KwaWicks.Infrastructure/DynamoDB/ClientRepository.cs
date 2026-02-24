using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Infrastructure.DynamoDB;

public class ClientRepository : IClientRepository
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _tableName;

    public ClientRepository(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _tableName = tableName;
    }

    private static string Pk(string clientId) => $"CLIENT#{clientId}";
    private const string SkValue = "PROFILE";

    public async Task PutAsync(Client client, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = Pk(client.ClientId) },
            ["SK"] = new AttributeValue { S = SkValue },

            ["EntityType"] = new AttributeValue { S = "Client" },

            ["ClientId"] = new AttributeValue { S = client.ClientId },
            ["ClientName"] = new AttributeValue { S = client.ClientName },
            ["ClientAddress"] = new AttributeValue { S = client.ClientAddress ?? "" },
            ["ClientContactDetails"] = new AttributeValue { S = client.ClientContactDetails ?? "" },
            ["ClientType"] = new AttributeValue { S = client.ClientType.ToString() },

            ["CreatedAtUtc"] = new AttributeValue { S = client.CreatedAtUtc.ToString("O") },
            ["UpdatedAtUtc"] = new AttributeValue { S = client.UpdatedAtUtc.ToString("O") }
        };

        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        }, ct);
    }

    public async Task<Client?> GetAsync(string clientId, CancellationToken ct = default)
    {
        var res = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(clientId) },
                ["SK"] = new AttributeValue { S = SkValue }
            }
        }, ct);

        if (res.Item is null || res.Item.Count == 0) return null;
        return FromItem(res.Item);
    }

    public async Task<List<Client>> ListAsync(int limit = 50, CancellationToken ct = default)
    {
        // Simple scan by EntityType.
        // If you want this to scale, we can add a GSI like GSI1PK="CLIENT" later.
        var res = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            Limit = Math.Clamp(limit, 1, 200),
            FilterExpression = "EntityType = :t AND SK = :sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":t"] = new AttributeValue { S = "Client" },
                [":sk"] = new AttributeValue { S = SkValue }
            }
        }, ct);

        return res.Items.Select(FromItem).ToList();
    }

    public async Task<bool> DeleteAsync(string clientId, CancellationToken ct = default)
    {
        await _ddb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = Pk(clientId) },
                ["SK"] = new AttributeValue { S = SkValue }
            }
        }, ct);

        return true;
    }

    private static Client FromItem(Dictionary<string, AttributeValue> item)
    {
        item.TryGetValue("ClientType", out var ctVal);

        Enum.TryParse<ClientType>(ctVal?.S, ignoreCase: true, out var clientType);

        DateTime.TryParse(item.GetValueOrDefault("CreatedAtUtc")?.S, out var created);
        DateTime.TryParse(item.GetValueOrDefault("UpdatedAtUtc")?.S, out var updated);

        return new Client
        {
            ClientId = item["ClientId"].S,
            ClientName = item.GetValueOrDefault("ClientName")?.S ?? "",
            ClientAddress = item.GetValueOrDefault("ClientAddress")?.S ?? "",
            ClientContactDetails = item.GetValueOrDefault("ClientContactDetails")?.S ?? "",
            ClientType = clientType,
            CreatedAtUtc = created == default ? DateTime.UtcNow : created,
            UpdatedAtUtc = updated == default ? DateTime.UtcNow : updated
        };
    }
}
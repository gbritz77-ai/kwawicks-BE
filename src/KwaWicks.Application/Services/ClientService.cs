using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _repo;

    public ClientService(IClientRepository repo)
    {
        _repo = repo;
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
            throw new ArgumentException("ClientName is required.");

        var now = DateTime.UtcNow;

        var client = new Client
        {
            ClientId = Guid.NewGuid().ToString("N"),
            ClientName = request.ClientName.Trim(),
            ClientAddress = request.ClientAddress?.Trim() ?? "",
            ClientContactDetails = request.ClientContactDetails?.Trim() ?? "",
            ClientType = request.ClientType,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _repo.PutAsync(client, ct);
        return Map(client);
    }

    public async Task<ClientDto?> GetByIdAsync(string clientId, CancellationToken ct = default)
    {
        var client = await _repo.GetAsync(clientId, ct);
        return client is null ? null : Map(client);
    }

    public async Task<List<ClientDto>> ListAsync(int limit = 50, CancellationToken ct = default)
    {
        var clients = await _repo.ListAsync(limit, ct);
        return clients.Select(Map).ToList();
    }

    public async Task<ClientDto?> UpdateAsync(string clientId, UpdateClientRequest request, CancellationToken ct = default)
    {
        var existing = await _repo.GetAsync(clientId, ct);
        if (existing is null) return null;

        existing.ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? existing.ClientName : request.ClientName.Trim();
        existing.ClientAddress = request.ClientAddress?.Trim() ?? "";
        existing.ClientContactDetails = request.ClientContactDetails?.Trim() ?? "";
        existing.ClientType = request.ClientType;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.PutAsync(existing, ct);
        return Map(existing);
    }

    public async Task<bool> DeleteAsync(string clientId, CancellationToken ct = default)
        => await _repo.DeleteAsync(clientId, ct);

    private static ClientDto Map(Client c) => new()
    {
        ClientId = c.ClientId,
        ClientName = c.ClientName,
        ClientAddress = c.ClientAddress,
        ClientContactDetails = c.ClientContactDetails,
        ClientType = c.ClientType,
        CreatedAtUtc = c.CreatedAtUtc,
        UpdatedAtUtc = c.UpdatedAtUtc
    };
}
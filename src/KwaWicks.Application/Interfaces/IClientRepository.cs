using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IClientRepository
{
    Task PutAsync(Client client, CancellationToken ct = default);
    Task<Client?> GetAsync(string clientId, CancellationToken ct = default);
    Task<List<Client>> ListAsync(int limit = 50, CancellationToken ct = default);
    Task<bool> DeleteAsync(string clientId, CancellationToken ct = default);
}
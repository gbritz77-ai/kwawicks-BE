using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IClientCreditRepository
{
    Task<ClientCreditEntry> AddEntryAsync(ClientCreditEntry entry, CancellationToken ct = default);
    Task<List<ClientCreditEntry>> ListByClientAsync(string clientId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(string clientId, CancellationToken ct = default);
}

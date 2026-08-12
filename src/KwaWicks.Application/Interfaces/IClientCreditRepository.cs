using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IClientCreditRepository
{
    Task<ClientCreditEntry> AddEntryAsync(ClientCreditEntry entry, CancellationToken ct = default);
    Task<List<ClientCreditEntry>> ListByClientAsync(string clientId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(string clientId, CancellationToken ct = default);
    Task<decimal> SumCashDepositsAsync(DateTime? since, CancellationToken ct = default);
    Task DeleteEntryAsync(string entryId, CancellationToken ct = default);
    Task<ClientCreditEntry?> FindInvoiceChargeAsync(string invoiceId, CancellationToken ct = default);
    Task UpdateEntryAmountAsync(string entryId, decimal newAmount, CancellationToken ct = default);
    Task<List<ClientCreditEntry>> ListAllAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}

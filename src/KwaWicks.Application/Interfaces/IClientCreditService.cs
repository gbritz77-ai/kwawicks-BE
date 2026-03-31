using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IClientCreditService
{
    Task<ClientCreditEntryResponse> AddDepositAsync(string clientId, AddCreditDepositRequest request, CancellationToken ct = default);
    Task<ClientCreditEntryResponse> ChargeInvoiceAsync(string clientId, string invoiceId, decimal amount, CancellationToken ct = default);
    Task<ClientCreditLedgerResponse> GetLedgerAsync(string clientId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(string clientId, CancellationToken ct = default);
}

using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class ClientCreditService : IClientCreditService
{
    private readonly IClientCreditRepository _repo;
    private readonly IClientRepository _clientRepo;

    public ClientCreditService(IClientCreditRepository repo, IClientRepository clientRepo)
    {
        _repo = repo;
        _clientRepo = clientRepo;
    }

    public async Task<ClientCreditEntryResponse> AddDepositAsync(
        string clientId, AddCreditDepositRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Deposit amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException("Payment method is required for deposits.");

        var entry = new ClientCreditEntry
        {
            ClientId        = clientId,
            Amount          = request.Amount,
            EntryType       = "Deposit",
            PaymentMethod   = request.PaymentMethod,
            Notes           = request.Notes,
            CreatedByUserId = request.CreatedByUserId,
            Reference       = "",
        };

        await _repo.AddEntryAsync(entry, ct);
        return Map(entry);
    }

    public async Task<ClientCreditEntryResponse> ChargeInvoiceAsync(
        string clientId, string invoiceId, decimal amount, CancellationToken ct = default)
    {
        var entry = new ClientCreditEntry
        {
            ClientId        = clientId,
            Amount          = -Math.Abs(amount), // always negative (debit)
            EntryType       = "InvoiceCharge",
            PaymentMethod   = "",
            Reference       = invoiceId,
            Notes           = $"Invoice {invoiceId.Substring(0, Math.Min(8, invoiceId.Length)).ToUpper()} charged to account",
            CreatedByUserId = "system",
        };

        await _repo.AddEntryAsync(entry, ct);
        return Map(entry);
    }

    public async Task<ClientCreditLedgerResponse> GetLedgerAsync(
        string clientId, CancellationToken ct = default)
    {
        var client  = await _clientRepo.GetAsync(clientId, ct);
        var entries = await _repo.ListByClientAsync(clientId, ct);
        var balance = entries.Sum(e => e.Amount);

        return new ClientCreditLedgerResponse
        {
            ClientId   = clientId,
            ClientName = client?.ClientName ?? clientId,
            Balance    = balance,
            Entries    = entries.Select(Map).ToList(),
        };
    }

    public Task<decimal> GetBalanceAsync(string clientId, CancellationToken ct = default)
        => _repo.GetBalanceAsync(clientId, ct);

    private static ClientCreditEntryResponse Map(ClientCreditEntry e) => new()
    {
        EntryId         = e.EntryId,
        ClientId        = e.ClientId,
        Amount          = e.Amount,
        EntryType       = e.EntryType,
        PaymentMethod   = e.PaymentMethod,
        Reference       = e.Reference,
        Notes           = e.Notes,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt       = e.CreatedAt,
    };
}

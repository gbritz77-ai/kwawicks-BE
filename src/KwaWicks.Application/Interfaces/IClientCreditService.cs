using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IClientCreditService
{
    Task<ClientCreditEntryResponse> AddDepositAsync(string clientId, AddCreditDepositRequest request, CancellationToken ct = default);
    Task<ClientCreditEntryResponse> ChargeInvoiceAsync(string clientId, string invoiceId, decimal amount, CancellationToken ct = default);
    /// <summary>Reverses a prior InvoiceCharge (e.g. when a Credit invoice is cancelled). Posts a positive ledger entry.</summary>
    Task<ClientCreditEntryResponse> ReverseInvoiceChargeAsync(string clientId, string invoiceId, decimal amount, CancellationToken ct = default);
    /// <summary>Records money actually received against an invoice (Cash/EFT/Card/etc). Posts a positive ledger entry
    /// distinct from a manual Deposit, so the client's statement shows the payment tied to that specific invoice.
    /// Pass occurredAt for bank-reconciled payments so the ledger reflects the bank statement's transaction date,
    /// not the date staff happened to process the reconciliation.</summary>
    Task<ClientCreditEntryResponse> RecordInvoicePaymentAsync(string clientId, string invoiceId, decimal amount, string paymentMethod, CancellationToken ct = default, DateTime? occurredAt = null);
    /// <summary>Reverses a previously recorded InvoicePayment (e.g. a bank allocation is undone). Posts a negative ledger entry.</summary>
    Task<ClientCreditEntryResponse> ReverseInvoicePaymentAsync(string clientId, string invoiceId, decimal amount, CancellationToken ct = default);
    Task<ClientCreditEntryResponse> AddManualChargeAsync(string clientId, decimal amount, string notes, string createdByUserId, CancellationToken ct = default);
    Task DeleteEntryAsync(string clientId, string entryId, CancellationToken ct = default);
    Task<ClientCreditLedgerResponse> GetLedgerAsync(string clientId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(string clientId, CancellationToken ct = default);
    Task<CreditProofUploadUrlResponse> GetProofUploadUrlAsync(string clientId, string contentType, CancellationToken ct = default);
}

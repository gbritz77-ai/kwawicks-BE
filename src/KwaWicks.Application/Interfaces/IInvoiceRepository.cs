using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice> CreateAsync(Invoice invoice, CancellationToken ct);
    Task<Invoice?> GetAsync(string invoiceId, CancellationToken ct);
    Task<Invoice> UpdateAsync(Invoice invoice, CancellationToken ct);
    Task<List<Invoice>> ListAsync(string? hubId, string? customerId, CancellationToken ct);
    Task<string> GetNextInvoiceNumberAsync(CancellationToken ct);
    Task<decimal> SumCashSalesAsync(DateTime? since, CancellationToken ct);
    Task<List<Invoice>> ListByPriceApprovalStatusAsync(string status, CancellationToken ct);
    Task<List<Invoice>> ListForReconAsync(string? paymentType, DateTime? from, DateTime? to, CancellationToken ct);
}

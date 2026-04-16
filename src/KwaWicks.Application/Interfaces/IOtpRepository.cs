using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IOtpRepository
{
    Task SaveAsync(OtpRecord record, CancellationToken ct);
    Task<OtpRecord?> GetByInvoiceIdAsync(string invoiceId, CancellationToken ct);
    Task<List<OtpRecord>> ListByClientAsync(string clientId, CancellationToken ct);
    Task<List<OtpRecord>> ListAllAsync(DateTime? from, DateTime? to, CancellationToken ct);
}

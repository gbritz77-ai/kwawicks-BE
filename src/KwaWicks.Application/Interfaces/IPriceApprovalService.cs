using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IPriceApprovalService
{
    /// <summary>
    /// Checks invoice lines against species cost. If any line is below cost,
    /// flags the invoice as PendingApproval and sends WhatsApp alerts to admins.
    /// Returns true if the invoice was flagged.
    /// </summary>
    Task<bool> CheckAndFlagAsync(string invoiceId, CancellationToken ct);

    Task<List<PriceApprovalResponse>> GetPendingAsync(CancellationToken ct);

    Task ApproveAsync(string invoiceId, CancellationToken ct);

    Task AmendAndApproveAsync(string invoiceId, AmendPriceRequest request, CancellationToken ct);
}

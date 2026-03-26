namespace KwaWicks.Application.Interfaces;

public interface IInvoiceNotificationService
{
    /// <summary>
    /// Generates the invoice PDF, uploads to S3, and sends via WhatsApp.
    /// Returns (true, null) on success or (false, errorMessage) on failure.
    /// Never throws — failures are returned as a result so invoice creation is unaffected.
    /// </summary>
    Task<(bool Sent, string? Error)> TrySendInvoiceWhatsAppAsync(
        string invoiceId,
        string phone,
        CancellationToken ct = default);
}

namespace KwaWicks.Application.Interfaces;

public interface IWhatsAppService
{
    Task SendInvoiceAsync(
        string toPhone,
        string recipientName,
        string invoiceNumber,
        decimal grandTotal,
        string pdfUrl,
        string pdfFilename,
        CancellationToken ct = default);

    Task SendStatementAsync(
        string toPhone,
        string recipientName,
        string pdfUrl,
        string pdfFilename,
        CancellationToken ct = default);
}

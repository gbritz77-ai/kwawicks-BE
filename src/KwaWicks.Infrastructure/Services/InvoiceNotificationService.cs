using KwaWicks.Application.Interfaces;

namespace KwaWicks.Infrastructure.Services;

public class InvoiceNotificationService : IInvoiceNotificationService
{
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;
    private readonly ISpeciesRepository _speciesRepository;
    private readonly IPdfService _pdfService;
    private readonly IS3Service _s3Service;
    private readonly IWhatsAppService _whatsAppService;

    public InvoiceNotificationService(
        IInvoiceService invoiceService,
        IClientService clientService,
        ISpeciesRepository speciesRepository,
        IPdfService pdfService,
        IS3Service s3Service,
        IWhatsAppService whatsAppService)
    {
        _invoiceService = invoiceService;
        _clientService = clientService;
        _speciesRepository = speciesRepository;
        _pdfService = pdfService;
        _s3Service = s3Service;
        _whatsAppService = whatsAppService;
    }

    public async Task<(bool Sent, string? Error)> TrySendInvoiceWhatsAppAsync(
        string invoiceId, string phone, CancellationToken ct = default)
    {
        try
        {
            var invoice = await _invoiceService.GetAsync(invoiceId, ct);
            if (invoice is null) return (false, "Invoice not found");

            var client = await _clientService.GetByIdAsync(invoice.CustomerId, ct);
            if (client is null) return (false, "Client not found");

            var speciesIds = invoice.Lines.Select(l => l.SpeciesId).Distinct().ToList();
            var speciesNames = new List<(string speciesId, string speciesName)>();
            foreach (var sid in speciesIds)
            {
                var species = await _speciesRepository.GetAsync(sid, ct);
                speciesNames.Add((sid, species?.Name ?? sid));
            }

            byte[]? sigBytes = null;
            if (!string.IsNullOrEmpty(invoice.ClientSignatureS3Key))
            {
                try { sigBytes = await _s3Service.DownloadObjectAsync(invoice.ClientSignatureS3Key, ct); }
                catch (Exception sigEx)
                {
                    // Log to response so caller can diagnose without CloudWatch access
                    Console.Error.WriteLine($"[Signature] Failed to download {invoice.ClientSignatureS3Key}: {sigEx.Message}");
                }
            }

            var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice, client, speciesNames, sigBytes, ct);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var s3Key = $"pdfs/invoice-{invoiceId}-{timestamp}.pdf";
            await _s3Service.UploadObjectAsync(s3Key, pdfBytes, "application/pdf", ct);
            var pdfUrl = await _s3Service.GeneratePresignedViewUrlAsync(s3Key, 5, ct);

            var invoiceNumber = invoiceId[..Math.Min(8, invoiceId.Length)];
            var filename = $"KwaWicks-Invoice-{invoiceNumber}.pdf";

            await _whatsAppService.SendInvoiceAsync(
                phone,
                client.ClientName,
                invoiceNumber,
                invoice.GrandTotal,
                pdfUrl,
                filename,
                ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

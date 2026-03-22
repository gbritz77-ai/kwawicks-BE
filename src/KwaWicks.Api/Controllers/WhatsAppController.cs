using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Produces("application/json")]
[Authorize]
public class WhatsAppController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;
    private readonly IReportService _reportService;
    private readonly ISpeciesRepository _speciesRepository;
    private readonly IPdfService _pdfService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IS3Service _s3Service;

    public WhatsAppController(
        IInvoiceService invoiceService,
        IClientService clientService,
        IReportService reportService,
        ISpeciesRepository speciesRepository,
        IPdfService pdfService,
        IWhatsAppService whatsAppService,
        IS3Service s3Service)
    {
        _invoiceService = invoiceService;
        _clientService = clientService;
        _reportService = reportService;
        _speciesRepository = speciesRepository;
        _pdfService = pdfService;
        _whatsAppService = whatsAppService;
        _s3Service = s3Service;
    }

    // POST /api/whatsapp/invoice/{invoiceId}
    [HttpPost("invoice/{invoiceId}")]
    [Authorize(Policy = "FinancialAccess")]
    [ProducesResponseType(typeof(WhatsAppSendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendInvoice(
        string invoiceId,
        [FromBody] SendInvoiceWhatsAppRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { error = "Phone number is required." });

        var invoice = await _invoiceService.GetAsync(invoiceId, ct);
        if (invoice is null)
            return NotFound(new { error = "Invoice not found." });

        var client = await _clientService.GetByIdAsync(invoice.CustomerId, ct);
        if (client is null)
            return NotFound(new { error = "Client not found." });

        // Gather species names
        var speciesIds = invoice.Lines.Select(l => l.SpeciesId).Distinct().ToList();
        var speciesNames = new List<(string speciesId, string speciesName)>();
        foreach (var sid in speciesIds)
        {
            var species = await _speciesRepository.GetAsync(sid, ct);
            speciesNames.Add((sid, species?.Name ?? sid));
        }

        // Generate PDF
        var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice, client, speciesNames, ct);

        // Upload to S3
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var s3Key = $"pdfs/invoice-{invoiceId}-{timestamp}.pdf";
        await _s3Service.UploadObjectAsync(s3Key, pdfBytes, "application/pdf", ct);

        // Generate 5-minute pre-signed URL
        var pdfUrl = await _s3Service.GeneratePresignedViewUrlAsync(s3Key, 5, ct);
        var filename = $"KwaWicks-Invoice-{invoiceId[..Math.Min(8, invoiceId.Length)]}.pdf";

        // Send WhatsApp
        var invoiceNumber = invoiceId[..Math.Min(8, invoiceId.Length)];
        await _whatsAppService.SendInvoiceAsync(
            request.Phone,
            client.ClientName,
            invoiceNumber,
            invoice.GrandTotal,
            pdfUrl,
            filename,
            ct);

        return Ok(new WhatsAppSendResult
        {
            Success = true,
            Message = $"Invoice sent via WhatsApp to {request.Phone}"
        });
    }

    // POST /api/whatsapp/statement/{clientId}
    [HttpPost("statement/{clientId}")]
    [Authorize(Policy = "FinancialAccess")]
    [ProducesResponseType(typeof(WhatsAppSendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendStatement(
        string clientId,
        [FromBody] SendStatementWhatsAppRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { error = "Phone number is required." });

        var client = await _clientService.GetByIdAsync(clientId, ct);
        if (client is null)
            return NotFound(new { error = "Client not found." });

        DateTime? from = request.From is not null ? DateTime.Parse(request.From) : null;
        DateTime? to = request.To is not null ? DateTime.Parse(request.To) : null;

        var statement = await _reportService.GetCustomerStatementAsync(clientId, from, to, ct);

        // Generate PDF
        var pdfBytes = await _pdfService.GenerateStatementPdfAsync(statement, ct);

        // Upload to S3
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var s3Key = $"pdfs/statement-{clientId}-{timestamp}.pdf";
        await _s3Service.UploadObjectAsync(s3Key, pdfBytes, "application/pdf", ct);

        // Generate 5-minute pre-signed URL
        var pdfUrl = await _s3Service.GeneratePresignedViewUrlAsync(s3Key, 5, ct);
        var filename = $"KwaWicks-Statement-{client.ClientName.Replace(" ", "-")}.pdf";

        // Send WhatsApp
        await _whatsAppService.SendStatementAsync(
            request.Phone,
            client.ClientName,
            pdfUrl,
            filename,
            ct);

        return Ok(new WhatsAppSendResult
        {
            Success = true,
            Message = $"Statement sent via WhatsApp to {request.Phone}"
        });
    }
}

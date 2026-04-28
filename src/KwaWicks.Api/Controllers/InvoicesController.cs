using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    private readonly IClientService _clientService;
    private readonly IInvoiceNotificationService _notification;

    public InvoicesController(
        IInvoiceService service,
        IClientService clientService,
        IInvoiceNotificationService notification)
    {
        _service = service;
        _clientService = clientService;
        _notification = notification;
    }

    // POST /api/invoices  (hub-side direct invoice creation)
    [HttpPost]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        try
        {
            var invoiceId = await _service.CreateInvoiceAsync(request, ct);

            // Resolve effective phone: use override if provided, else client's saved phone
            var effectivePhone = await ResolvePhoneAsync(request.CustomerId, request.ClientPhone, ct);

            bool whatsAppSent = false;
            string? whatsAppError = null;
            if (!string.IsNullOrWhiteSpace(effectivePhone))
            {
                (whatsAppSent, whatsAppError) = await _notification.TrySendInvoiceWhatsAppAsync(invoiceId, effectivePhone, ct);
            }

            return CreatedAtAction(nameof(GetById), new { invoiceId },
                new { invoiceId, whatsAppSent, whatsAppError });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<string?> ResolvePhoneAsync(string clientId, string? phoneOverride, CancellationToken ct)
    {
        var client = await _clientService.GetByIdAsync(clientId, ct);
        if (client is null) return null;

        // If a new phone was provided, save it to the client record
        if (!string.IsNullOrWhiteSpace(phoneOverride))
        {
            if (string.IsNullOrWhiteSpace(client.ClientPhone))
                await _clientService.PatchPhoneAsync(clientId, phoneOverride, ct);
            return phoneOverride;
        }

        // ClientPhone is the dedicated WhatsApp field; fall back to ClientContactDetails
        return !string.IsNullOrWhiteSpace(client.ClientPhone)
            ? client.ClientPhone
            : (!string.IsNullOrWhiteSpace(client.ClientContactDetails) ? client.ClientContactDetails : null);
    }

    // GET /api/invoices/{invoiceId}
    [HttpGet("{invoiceId}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InvoiceResponse>> GetById(string invoiceId, CancellationToken ct)
    {
        var invoice = await _service.GetAsync(invoiceId, ct);
        if (invoice is null) return NotFound();
        return Ok(invoice);
    }

    // GET /api/invoices?hubId=&customerId=
    [HttpGet]
    [ProducesResponseType(typeof(List<InvoiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<InvoiceResponse>>> List(
        [FromQuery] string? hubId,
        [FromQuery] string? customerId,
        CancellationToken ct)
    {
        var invoices = await _service.ListAsync(hubId, customerId, ct);
        return Ok(invoices);
    }

    // POST /api/invoices/{invoiceId}/payment
    [HttpPost("{invoiceId}/payment")]
    [Authorize(Policy = "DriverOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordPayment(
        string invoiceId,
        [FromBody] RecordPaymentRequest request,
        CancellationToken ct)
    {
        try
        {
            await _service.RecordPaymentAsync(invoiceId, request, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // PUT /api/invoices/{invoiceId}/confirm-payment  (Admin confirms receipt of payment)
    [HttpPut("{invoiceId}/confirm-payment")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmPayment(string invoiceId, CancellationToken ct)
    {
        try
        {
            await _service.ConfirmPaymentAsync(invoiceId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // GET /api/invoices/{invoiceId}/receipt-view-url  (Admin views uploaded receipt)
    [HttpGet("{invoiceId}/receipt-view-url")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptViewUrl(string invoiceId, CancellationToken ct)
    {
        try
        {
            var url = await _service.GetReceiptViewUrlAsync(invoiceId, ct);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // PATCH /api/invoices/{invoiceId}/lines  (Owner: update prices and resend WhatsApp)
    [HttpPatch("{invoiceId}/lines")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdateLines(string invoiceId, [FromBody] UpdateInvoiceLinesRequest request, CancellationToken ct)
    {
        try
        {
            var invoice = await _service.UpdateLinesAsync(invoiceId, request, ct);

            // Resolve client phone and immediately resend the updated invoice via WhatsApp
            var effectivePhone = await ResolvePhoneAsync(invoice.CustomerId, null, ct);
            bool whatsAppSent = false;
            string? whatsAppError = null;
            if (!string.IsNullOrWhiteSpace(effectivePhone))
            {
                (whatsAppSent, whatsAppError) = await _notification.TrySendInvoiceWhatsAppAsync(invoiceId, effectivePhone, ct);
            }

            return Ok(new UpdateInvoiceLinesResponse
            {
                Invoice = invoice,
                WhatsAppSent = whatsAppSent,
                WhatsAppError = whatsAppError
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // GET /api/invoices/recon?paymentType=&reconStatus=&from=&to=
    [HttpGet("recon")]
    [Authorize(Policy = "FinancialAccess")]
    [ProducesResponseType(typeof(List<ReconInvoiceItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReconList(
        [FromQuery] string? paymentType,
        [FromQuery] string? reconStatus,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var items = await _service.GetReconListAsync(paymentType, reconStatus, from, to, ct);

        // Enrich with customer names in-memory using the already-injected client service
        var clients = await _clientService.ListAsync(1000, ct);
        var clientMap = clients.ToDictionary(c => c.ClientId, c => c.ClientName);
        foreach (var item in items)
            item.CustomerName = clientMap.TryGetValue(item.CustomerId, out var name) ? name : item.CustomerId;

        return Ok(items);
    }

    // PUT /api/invoices/{invoiceId}/recon
    [HttpPut("{invoiceId}/recon")]
    [Authorize(Policy = "FinancialAccess")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recon(string invoiceId, [FromBody] ReconRequest request, CancellationToken ct)
    {
        try
        {
            await _service.ReconAsync(invoiceId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // GET /api/invoices/{invoiceId}/receipt-upload-url
    [HttpGet("{invoiceId}/receipt-upload-url")]
    [Authorize(Policy = "DriverOnly")]
    [ProducesResponseType(typeof(ReceiptUploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReceiptUploadUrlResponse>> GetReceiptUploadUrl(string invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetReceiptUploadUrlAsync(invoiceId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

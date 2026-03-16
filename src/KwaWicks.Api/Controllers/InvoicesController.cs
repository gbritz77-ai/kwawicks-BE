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

    public InvoicesController(IInvoiceService service)
    {
        _service = service;
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
            return CreatedAtAction(nameof(GetById), new { invoiceId }, new { invoiceId });
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

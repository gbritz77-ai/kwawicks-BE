using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

// ── Request DTOs ─────────────────────────────────────────────────────────────

public class DriverSaleLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }   // ex-VAT (back-calculated by frontend)
    public decimal VatRate { get; set; } = 0.15m;
}

public class CreateDriverSaleRequest
{
    /// <summary>Existing client ID. Set this OR provide NewClient, not both.</summary>
    public string? CustomerId { get; set; }

    /// <summary>New walk-in customer. Saved with IsWalkIn=true.</summary>
    public CreateClientRequest? NewClient { get; set; }

    public string HubId { get; set; } = "";
    public string PaymentType { get; set; } = "Cash"; // Cash | EFT | Card | AccountCredit
    public string? ClientPhone { get; set; }
    public List<DriverSaleLineRequest> Lines { get; set; } = new();
}

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/driver-sales")]
[Produces("application/json")]
[Authorize(Policy = "DriverOnly")]
public class DriverSalesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;
    private readonly IInvoiceNotificationService _notification;
    private readonly IClientCreditService _creditService;

    public DriverSalesController(
        IInvoiceService invoiceService,
        IClientService clientService,
        IInvoiceNotificationService notification,
        IClientCreditService creditService)
    {
        _invoiceService = invoiceService;
        _clientService = clientService;
        _notification = notification;
        _creditService = creditService;
    }

    // POST /api/driver-sales
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDriverSaleRequest request, CancellationToken ct)
    {
        try
        {
            if (request.Lines is null || request.Lines.Count == 0)
                return BadRequest(new { error = "At least one line is required." });

            // 1. Resolve or create client
            string customerId;
            string? effectivePhone = request.ClientPhone?.Trim();

            if (!string.IsNullOrWhiteSpace(request.CustomerId))
            {
                customerId = request.CustomerId;
                if (string.IsNullOrWhiteSpace(effectivePhone))
                {
                    var client = await _clientService.GetByIdAsync(customerId, ct);
                    effectivePhone = !string.IsNullOrWhiteSpace(client?.ClientPhone)
                        ? client.ClientPhone
                        : client?.ClientContactDetails;
                }
            }
            else if (request.NewClient is not null)
            {
                request.NewClient.IsWalkIn = true;
                var newClient = await _clientService.CreateAsync(request.NewClient, ct);
                customerId = newClient.ClientId;
                effectivePhone = null; // walk-ins have no phone on record
            }
            else
            {
                return BadRequest(new { error = "Provide customerId or newClient." });
            }

            // 2. Build invoice request
            var invoiceReq = new CreateInvoiceRequest
            {
                CustomerId = customerId,
                HubId = request.HubId,
                PaymentType = request.PaymentType,
                SaleType = "DriverDirect",
                StaffMemberId = "",
                Lines = request.Lines.Select(l => new CreateInvoiceLine
                {
                    SpeciesId = l.SpeciesId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate
                }).ToList()
            };

            var invoiceId = await _invoiceService.CreateInvoiceAsync(invoiceReq, ct);

            // 3a. Immediately confirm Cash / EFT / Card payments — no Finance sign-off needed
            var immediatePayTypes = new[] { "Cash", "EFT", "Card", "CardMachine" };
            if (immediatePayTypes.Contains(request.PaymentType))
            {
                await _invoiceService.ConfirmPaymentAsync(invoiceId, ct);
            }

            // 3b. Auto-debit client credit if chosen
            bool creditCharged = false;
            decimal newCreditBalance = 0m;
            if (request.PaymentType == "AccountCredit" && !string.IsNullOrWhiteSpace(customerId))
            {
                var invoice = await _invoiceService.GetAsync(invoiceId, ct);
                if (invoice != null)
                {
                    await _creditService.ChargeInvoiceAsync(customerId, invoiceId, invoice.GrandTotal, ct);
                    newCreditBalance = await _creditService.GetBalanceAsync(customerId, ct);
                    creditCharged = true;
                }
            }

            // 4. Send WhatsApp invoice
            bool whatsAppSent = false;
            string? whatsAppError = null;
            if (!string.IsNullOrWhiteSpace(effectivePhone))
            {
                (whatsAppSent, whatsAppError) = await _notification.TrySendInvoiceWhatsAppAsync(invoiceId, effectivePhone, ct);
            }

            return CreatedAtAction(nameof(GetById), new { invoiceId },
                new { invoiceId, whatsAppSent, whatsAppError, creditCharged, newCreditBalance });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/driver-sales/{invoiceId}
    [HttpGet("{invoiceId}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string invoiceId, CancellationToken ct)
    {
        var inv = await _invoiceService.GetAsync(invoiceId, ct);
        if (inv is null) return NotFound();
        return Ok(inv);
    }
}

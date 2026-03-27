using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

// ── Request DTOs ────────────────────────────────────────────────────────────

public class HubSaleLineRequest
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }   // adjustable at checkout
    public decimal VatRate { get; set; } = 0.15m;
}

public class CreateHubSaleRequest
{
    /// <summary>Existing client ID. Set this OR provide NewClient, not both.</summary>
    public string? CustomerId { get; set; }

    /// <summary>New walk-in customer. Saved with IsWalkIn=true.</summary>
    public CreateClientRequest? NewClient { get; set; }

    /// <summary>Staff member buying on account. If set, payment is deferred (OnAccount).</summary>
    public string? StaffMemberId { get; set; }

    public string HubId { get; set; } = "";
    public string PaymentType { get; set; } = "Cash"; // Cash | EFT | Card | OnAccount
    public string? ClientPhone { get; set; }
    public List<HubSaleLineRequest> Lines { get; set; } = new();
}

// ── Controller ──────────────────────────────────────────────────────────────

[ApiController]
[Route("api/hub-sales")]
[Produces("application/json")]
[Authorize(Policy = "HubStaffOnly")]
public class HubSalesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;
    private readonly IStaffMemberService _staffService;
    private readonly IInvoiceNotificationService _notification;

    public HubSalesController(
        IInvoiceService invoiceService,
        IClientService clientService,
        IStaffMemberService staffService,
        IInvoiceNotificationService notification)
    {
        _invoiceService = invoiceService;
        _clientService = clientService;
        _staffService = staffService;
        _notification = notification;
    }

    // POST /api/hub-sales
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateHubSaleRequest request, CancellationToken ct)
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
                // Resolve phone from client if not provided
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
                if (string.IsNullOrWhiteSpace(effectivePhone))
                    effectivePhone = newClient.ClientPhone?.Trim().Length > 0
                        ? newClient.ClientPhone
                        : newClient.ClientContactDetails;
            }
            else if (!string.IsNullOrWhiteSpace(request.StaffMemberId))
            {
                var staff = await _staffService.GetByIdAsync(request.StaffMemberId, ct);
                if (staff is null) return BadRequest(new { error = "Staff member not found." });
                // Staff buy on account — no WhatsApp invoice, only monthly statement
                customerId = request.StaffMemberId; // staff ID used as customerId for their invoices
                effectivePhone = null;
            }
            else
            {
                return BadRequest(new { error = "Provide customerId, newClient, or staffMemberId." });
            }

            // 2. Build CreateInvoiceRequest
            var invoiceReq = new CreateInvoiceRequest
            {
                CustomerId = customerId,
                HubId = request.HubId,
                PaymentType = request.PaymentType,
                SaleType = "HubDirect",
                StaffMemberId = request.StaffMemberId ?? "",
                Lines = request.Lines.Select(l => new CreateInvoiceLine
                {
                    SpeciesId = l.SpeciesId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate
                }).ToList()
            };

            var invoiceId = await _invoiceService.CreateInvoiceAsync(invoiceReq, ct);

            // 3. Auto-send WhatsApp for non-staff sales
            bool whatsAppSent = false;
            string? whatsAppError = null;
            if (!string.IsNullOrWhiteSpace(effectivePhone) && string.IsNullOrWhiteSpace(request.StaffMemberId))
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

    // GET /api/hub-sales/{invoiceId}
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

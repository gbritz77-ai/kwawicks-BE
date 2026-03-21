using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/delivery-orders")]
[Produces("application/json")]
[Authorize]
public class DeliveryOrdersController : ControllerBase
{
    private readonly IDeliveryOrderService _service;
    private readonly IInvoiceService _invoiceService;

    public DeliveryOrdersController(IDeliveryOrderService service, IInvoiceService invoiceService)
    {
        _service = service;
        _invoiceService = invoiceService;
    }

    // POST /api/delivery-orders
    [HttpPost]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryOrderRequest request, CancellationToken ct)
    {
        try
        {
            // Only Owner and Finance may override the unit price on lines
            bool hasPriceOverride = request.Lines?.Any(l => l.UnitPrice.HasValue) == true;
            if (hasPriceOverride)
            {
                var roles = User.FindAll("cognito:groups").Select(c => c.Value).ToHashSet();
                if (!roles.Contains("Owner") && !roles.Contains("Finance"))
                    return Forbid();
            }

            var id = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { deliveryOrderId = id }, new { deliveryOrderId = id });
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

    // GET /api/delivery-orders?driverId=&hubId=&status=
    [HttpGet]
    [ProducesResponseType(typeof(List<DeliveryOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<DeliveryOrderResponse>>> List(
        [FromQuery] string? driverId,
        [FromQuery] string? hubId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var orders = await _service.ListAsync(driverId, hubId, status, ct);
        return Ok(orders);
    }

    // GET /api/delivery-orders/{deliveryOrderId}
    [HttpGet("{deliveryOrderId}")]
    [ProducesResponseType(typeof(DeliveryOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeliveryOrderResponse>> GetById(string deliveryOrderId, CancellationToken ct)
    {
        var order = await _service.GetAsync(deliveryOrderId, ct);
        if (order is null) return NotFound();
        return Ok(order);
    }

    // PUT /api/delivery-orders/{deliveryOrderId}/status
    [HttpPut("{deliveryOrderId}/status")]
    [Authorize(Policy = "DriverOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(
        string deliveryOrderId,
        [FromBody] UpdateDeliveryStatusRequest request,
        CancellationToken ct)
    {
        try
        {
            await _service.UpdateStatusAsync(deliveryOrderId, request.Status, ct);
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

    // POST /api/delivery-orders/{deliveryOrderId}/invoice
    [HttpPost("{deliveryOrderId}/invoice")]
    [Authorize(Policy = "DriverOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInvoiceFromDelivery(
        string deliveryOrderId,
        [FromBody] CreateInvoiceFromDeliveryRequest request,
        CancellationToken ct)
    {
        try
        {
            var invoiceId = await _invoiceService.CreateFromDeliveryAsync(deliveryOrderId, request, ct);
            return CreatedAtAction(
                nameof(InvoicesController.GetById),
                "Invoices",
                new { invoiceId },
                new { invoiceId });
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
}

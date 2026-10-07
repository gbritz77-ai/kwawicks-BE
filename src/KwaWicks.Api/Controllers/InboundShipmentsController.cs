using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/inbound-shipments")]
[Produces("application/json")]
[Authorize]
public class InboundShipmentsController : ControllerBase
{
    private readonly IInboundShipmentService _service;
    public InboundShipmentsController(IInboundShipmentService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "OperationalAccess")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? supplierId, CancellationToken ct)
    {
        var result = await _service.ListAsync(status, supplierId, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "OperationalAccess")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "ProcurementAccess")]
    public async Task<IActionResult> Create([FromBody] CreateInboundShipmentRequest request, CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue("username") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _service.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.InboundShipmentId }, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ProcurementAccess")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateInboundShipmentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ProcurementAccess")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/inbound-shipments/{id}/dispatch — transporter has collected from supplier
    [HttpPut("{id}/dispatch")]
    [Authorize(Policy = "OperationalAccess")]
    public async Task<IActionResult> MarkDispatched(string id, CancellationToken ct)
    {
        try
        {
            await _service.MarkDispatchedAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/inbound-shipments/{id}/receive — hub staff checks in the arrived stock
    [HttpPost("{id}/receive")]
    [Authorize(Policy = "HubStaffOnly")]
    public async Task<IActionResult> Receive(string id, [FromBody] ReceiveInboundShipmentRequest request, CancellationToken ct)
    {
        try
        {
            await _service.ReceiveAsync(id, request, ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

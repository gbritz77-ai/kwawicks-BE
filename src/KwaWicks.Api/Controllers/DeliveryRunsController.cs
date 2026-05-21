using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/delivery-runs")]
public class DeliveryRunsController : ControllerBase
{
    private readonly IDeliveryRunService _service;
    public DeliveryRunsController(IDeliveryRunService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> List([FromQuery] string? driverId, [FromQuery] string? status, CancellationToken ct)
    {
        var result = await _service.ListAsync(driverId, status, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryRunRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.DeliveryRunId }, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("{id}/allocations")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddAllocation(string id, [FromBody] AddDeliveryRunAllocationRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.AddAllocationAsync(id, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpDelete("{id}/allocations/{deliveryOrderId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemoveAllocation(string id, string deliveryOrderId, CancellationToken ct)
    {
        try
        {
            var result = await _service.RemoveAllocationAsync(id, deliveryOrderId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("{id}/dispatch")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Dispatch(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.DispatchAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("{id}/allocations/{deliveryOrderId}/confirm-delivery")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ConfirmDelivery(
        string id,
        string deliveryOrderId,
        [FromBody] ConfirmDeliveryRunDeliveryRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.ConfirmDeliveryAsync(id, deliveryOrderId, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/driver-allocations")]
public class DriverStockAllocationsController : ControllerBase
{
    private readonly IDriverStockAllocationService _service;
    public DriverStockAllocationsController(IDriverStockAllocationService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "OperationalAccess")]
    public async Task<IActionResult> List([FromQuery] string? driverId, [FromQuery] string? status, CancellationToken ct)
    {
        var result = await _service.ListAsync(driverId, status, ct);
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
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateDriverStockAllocationRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPost("{id}/sale")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> RecordSale(string id, [FromBody] RecordDriverSaleRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.RecordSaleAsync(id, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/complete")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Complete(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.CompleteAsync(id, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Cancel(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.CancelAsync(id, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }
}

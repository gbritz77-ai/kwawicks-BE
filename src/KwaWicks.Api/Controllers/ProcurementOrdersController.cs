using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/procurement-orders")]
public class ProcurementOrdersController : ControllerBase
{
    private readonly IProcurementOrderService _service;
    public ProcurementOrdersController(IProcurementOrderService service) => _service = service;

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
    public async Task<IActionResult> Create([FromBody] CreateProcurementOrderRequest request, CancellationToken ct)
    {
        try
        {
            var userId = User.Identity?.Name ?? "";
            var result = await _service.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.ProcurementOrderId }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/submit")]
    [Authorize(Policy = "ProcurementAccess")]
    public async Task<IActionResult> Submit(string id, CancellationToken ct)
    {
        try
        {
            await _service.SubmitAsync(id, ct);
            return Ok();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/complete")]
    [Authorize(Policy = "FinancialAccess")]
    public async Task<IActionResult> Complete(string id, CancellationToken ct)
    {
        try
        {
            await _service.CompleteAsync(id, ct);
            return Ok();
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("{id}/invoice-upload-url")]
    [Authorize(Policy = "FinancialAccess")]
    public async Task<IActionResult> GetInvoiceUploadUrl(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetInvoiceUploadUrlAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }
}

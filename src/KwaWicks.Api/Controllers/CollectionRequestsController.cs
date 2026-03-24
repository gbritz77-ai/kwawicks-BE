using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/collection-requests")]
public class CollectionRequestsController : ControllerBase
{
    private readonly ICollectionRequestService _service;
    public CollectionRequestsController(ICollectionRequestService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = "OperationalAccess")]
    public async Task<IActionResult> List([FromQuery] string? driverId, [FromQuery] string? status, [FromQuery] string? procurementOrderId, CancellationToken ct)
    {
        var result = await _service.ListAsync(driverId, status, procurementOrderId, ct);
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
    [Authorize(Policy = "CollectionManagement")]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequestRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = result.CollectionRequestId }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/load")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> DriverLoad(string id, [FromBody] DriverLoadingUpdateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.DriverLoadAsync(id, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/dispatch")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> Dispatch(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.DispatchAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/arrive")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> Arrive(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.ArriveAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/hub-confirm")]
    [Authorize(Policy = "CollectionManagement")]
    public async Task<IActionResult> HubConfirm(string id, [FromBody] HubConfirmReceiptRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.HubConfirmAsync(id, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPut("{id}/finance-acknowledge")]
    [Authorize(Policy = "FinancialAccess")]
    public async Task<IActionResult> FinanceAcknowledge(string id, [FromBody] FinanceAcknowledgeRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.FinanceAcknowledgeAsync(id, request.InvoiceS3Key, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("{id}/delivery-note-view-url")]
    [Authorize(Policy = "FinancialAccess")]
    public async Task<IActionResult> GetDeliveryNoteViewUrl(string id, CancellationToken ct)
    {
        try
        {
            var viewUrl = await _service.GetDeliveryNoteViewUrlAsync(id, ct);
            return Ok(new { viewUrl });
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("{id}/delivery-note-upload-url")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> GetDeliveryNoteUploadUrl(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetDeliveryNoteUploadUrlAsync(id, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
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

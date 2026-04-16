using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/slaughter")]
[Authorize(Policy = "HubStaffOnly")]
public class SlaughterController : ControllerBase
{
    private readonly ISlaughterService _service;

    public SlaughterController(ISlaughterService service)
    {
        _service = service;
    }

    // POST /api/slaughter
    [HttpPost]
    [ProducesResponseType(typeof(SlaughterBatchResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSlaughterRequest request, CancellationToken ct)
    {
        try
        {
            var userId = User.Identity?.Name;
            var batch = await _service.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { batchId = batch.BatchId }, batch);
        }
        catch (ArgumentException ex)       { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex){ return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/slaughter
    [HttpGet]
    [ProducesResponseType(typeof(List<SlaughterBatchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var batches = await _service.ListAsync(ct);
        return Ok(batches);
    }

    // GET /api/slaughter/{batchId}
    [HttpGet("{batchId}")]
    [ProducesResponseType(typeof(SlaughterBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string batchId, CancellationToken ct)
    {
        var batch = await _service.GetAsync(batchId, ct);
        return batch is null ? NotFound() : Ok(batch);
    }
}

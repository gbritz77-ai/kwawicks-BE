using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/petty-cash")]
[Produces("application/json")]
[Authorize(Policy = "FinancialAccess")]
public class PettyCashController : ControllerBase
{
    private readonly IPettyCashService _service;

    public PettyCashController(IPettyCashService service)
    {
        _service = service;
    }

    // GET /api/petty-cash/summary
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PettyCashSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _service.GetSummaryAsync(ct);
        return Ok(summary);
    }

    // GET /api/petty-cash/entries?from=&to=
    [HttpGet("entries")]
    [ProducesResponseType(typeof(List<PettyCashEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEntries(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        var entries = await _service.ListEntriesAsync(from, to, ct);
        return Ok(entries);
    }

    // POST /api/petty-cash/entries
    [HttpPost("entries")]
    [ProducesResponseType(typeof(PettyCashEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntry([FromBody] CreatePettyCashEntryRequest request, CancellationToken ct)
    {
        try
        {
            var recordedBy = User.Identity?.Name ?? User.FindFirst("cognito:username")?.Value ?? "unknown";
            var dto = await _service.CreateEntryAsync(request, recordedBy, ct);
            return StatusCode(201, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/petty-cash/cashups
    [HttpGet("cashups")]
    [ProducesResponseType(typeof(List<PettyCashupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCashups(CancellationToken ct)
    {
        var cashups = await _service.ListCashupsAsync(ct);
        return Ok(cashups);
    }

    // POST /api/petty-cash/cashups
    [HttpPost("cashups")]
    [ProducesResponseType(typeof(PettyCashupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCashup([FromBody] CreateCashupRequest request, CancellationToken ct)
    {
        try
        {
            var closedBy = User.Identity?.Name ?? User.FindFirst("cognito:username")?.Value ?? "unknown";
            var dto = await _service.CreateCashupAsync(request, closedBy, ct);
            return StatusCode(201, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/cost-averages")]
[Authorize(Policy = "AdminOnly")]
public class CostAveragesController : ControllerBase
{
    private readonly ICostAverageService _service;

    public CostAveragesController(ICostAverageService service)
    {
        _service = service;
    }

    // POST /api/cost-averages/calculate
    /// <summary>
    /// Calculate weighted-average unit costs for all species with stock movements in
    /// the specified calendar month. Optionally writes results back to species.UnitCost.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(List<CostAverageRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculateCostAverageRequest request, CancellationToken ct)
    {
        try
        {
            var results = await _service.CalculateAsync(request, ct);
            return Ok(results);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/cost-averages?speciesId=
    /// <summary>
    /// Return the full history of cost-average records, optionally filtered to one species.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CostAverageRecordResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? speciesId, CancellationToken ct)
    {
        var records = await _service.GetHistoryAsync(speciesId, ct);
        return Ok(records);
    }
}

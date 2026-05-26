using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/stock-losses")]
[Authorize(Policy = "AdminOnly")]
public class StockLossController : ControllerBase
{
    private readonly IStockLossService _service;

    public StockLossController(IStockLossService service) => _service = service;

    // POST /api/stock-losses — record dead/lost stock
    [HttpPost]
    [ProducesResponseType(typeof(StockLossResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordLoss(
        [FromBody] RecordStockLossRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.FindFirstValue("username")
                      ?? User.FindFirstValue(ClaimTypes.Name)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? "admin";

            var result = await _service.RecordLossAsync(request, userId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)      { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)              { return StatusCode(500, new { error = ex.Message }); }
    }

    // GET /api/stock-losses?speciesId=&from=&to=
    [HttpGet]
    [ProducesResponseType(typeof(List<StockLossResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? speciesId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        try
        {
            var results = await _service.ListAsync(speciesId, from, to, ct);
            return Ok(results);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}

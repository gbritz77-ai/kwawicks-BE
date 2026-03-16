using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    // ── Admin ────────────────────────────────────────────────────────────────

    [HttpGet("revenue")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _reports.GetRevenueSummaryAsync(from, to, ct);
        return Ok(result);
    }

    [HttpGet("outstanding-payments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> OutstandingPayments(CancellationToken ct)
    {
        var result = await _reports.GetOutstandingPaymentsAsync(ct);
        return Ok(result);
    }

    [HttpGet("driver-performance")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DriverPerformance(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _reports.GetDriverPerformanceAsync(from, to, ct);
        return Ok(result);
    }

    [HttpGet("returns")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Returns(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _reports.GetReturnsSummaryAsync(from, to, ct);
        return Ok(result);
    }

    // ── Driver ───────────────────────────────────────────────────────────────

    [HttpGet("my-deliveries")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<IActionResult> MyDeliveries(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var driverId = User.FindFirstValue("username")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "";

        var result = await _reports.GetMyDeliveriesAsync(driverId, from, to, ct);
        return Ok(result);
    }
}

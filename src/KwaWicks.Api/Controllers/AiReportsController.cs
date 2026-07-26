using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/ai-reports")]
[Authorize(Policy = "FinancialAccess")]
public class AiReportsController : ControllerBase
{
    private readonly IAiReportService _service;

    public AiReportsController(IAiReportService service) => _service = service;

    // POST /api/ai-reports/query
    [HttpPost("query")]
    [ProducesResponseType(typeof(AiReportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query([FromBody] AiReportRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Prompt))
            return BadRequest(new { error = "Prompt is required." });

        var result = await _service.RunReportAsync(req.Prompt.Trim(), ct);
        return Ok(result);
    }
}

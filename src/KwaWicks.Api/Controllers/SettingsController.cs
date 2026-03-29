using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Policy = "FinancialAccess")]   // Owner + Finance can read
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _service;

    public SettingsController(ISettingsService service)
    {
        _service = service;
    }

    private string Caller =>
        User.Identity?.Name ?? User.FindFirst("cognito:username")?.Value ?? "unknown";

    // GET /api/settings
    [HttpGet]
    [ProducesResponseType(typeof(AppSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await _service.GetAsync(ct);
        return Ok(dto);
    }

    // PUT /api/settings  (Owner only)
    [HttpPut]
    [Authorize(Policy = "OwnerOnly")]
    [ProducesResponseType(typeof(AppSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateAppSettingsRequest request, CancellationToken ct)
    {
        var dto = await _service.UpdateAsync(request, Caller, ct);
        return Ok(dto);
    }
}

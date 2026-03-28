using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/hub-requests")]
[Authorize(Policy = "HubStaffOnly")]
public class HubRequestsController : ControllerBase
{
    private readonly IHubRequestService _service;

    public HubRequestsController(IHubRequestService service)
    {
        _service = service;
    }

    private string CallerUsername =>
        User.Identity?.Name ?? User.FindFirst("cognito:username")?.Value ?? "unknown";

    // POST /api/hub-requests  (Owner/Finance/Admin only)
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(HubRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateHubRequestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        try
        {
            var dto = await _service.CreateAsync(request, CallerUsername, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.HubRequestId }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/hub-requests
    [HttpGet]
    [ProducesResponseType(typeof(List<HubRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var items = await _service.ListAsync(status, ct);
        return Ok(items);
    }

    // GET /api/hub-requests/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HubRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        try
        {
            var dto = await _service.GetAsync(id, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // PUT /api/hub-requests/{id}/action  (any HubStaff+ can action)
    [HttpPut("{id}/action")]
    [ProducesResponseType(typeof(HubRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Action(string id, [FromBody] ActionHubRequestRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _service.ActionAsync(id, request, CallerUsername, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT /api/hub-requests/{id}/cancel  (Owner/Finance/Admin only)
    [HttpPut("{id}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(HubRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string id, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CancelAsync(id, CallerUsername, ct);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Services;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/species")]
public class SpeciesController : ControllerBase
{
    private readonly SpeciesService _service;

    public SpeciesController(SpeciesService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SpeciesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateSpeciesRequest? req, CancellationToken ct)
    {
        if (req is null) return BadRequest(new { error = "Request body is required." });

        try
        {
            var result = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { speciesId = result.SpeciesId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    [Authorize] // all authenticated users can read
    [ProducesResponseType(typeof(List<SpeciesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        return Ok(result);
    }

    [HttpGet("{speciesId}")]
    [Authorize]
    [ProducesResponseType(typeof(SpeciesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(string speciesId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            return BadRequest(new { error = "speciesId is required." });

        var result = await _service.GetAsync(speciesId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{speciesId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SpeciesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(string speciesId, [FromBody] UpdateSpeciesRequest? req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            return BadRequest(new { error = "speciesId is required." });

        if (req is null) return BadRequest(new { error = "Request body is required." });

        try
        {
            var result = await _service.UpdateAsync(speciesId, req, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

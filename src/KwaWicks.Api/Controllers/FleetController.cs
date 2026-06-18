using KwaWicks.Application.DTOs;
using KwaWicks.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/fleet")]
[Produces("application/json")]
[Authorize(Policy = "HubStaffOnly")]
public class FleetController : ControllerBase
{
    private readonly VehicleService _service;

    public FleetController(VehicleService service) => _service = service;

    // POST /api/fleet
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { vehicleId = dto.VehicleId }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/fleet
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
    {
        var list = await _service.ListAsync(search, ct);
        return Ok(list);
    }

    // GET /api/fleet/{vehicleId}
    [HttpGet("{vehicleId}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string vehicleId, CancellationToken ct)
    {
        var dto = await _service.GetAsync(vehicleId, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // PUT /api/fleet/{vehicleId}
    [HttpPut("{vehicleId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string vehicleId, [FromBody] UpdateVehicleRequest request, CancellationToken ct)
    {
        var dto = await _service.UpdateAsync(vehicleId, request, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // DELETE /api/fleet/{vehicleId}
    [HttpDelete("{vehicleId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(string vehicleId, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(vehicleId, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}

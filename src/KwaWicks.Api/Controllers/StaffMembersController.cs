using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/staff-members")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class StaffMembersController : ControllerBase
{
    private readonly IStaffMemberService _service;

    public StaffMembersController(IStaffMemberService service)
    {
        _service = service;
    }

    // POST /api/staff-members
    [HttpPost]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateStaffMemberRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { staffMemberId = dto.StaffMemberId }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/staff-members/{staffMemberId}
    [HttpGet("{staffMemberId}")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string staffMemberId, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(staffMemberId, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // GET /api/staff-members
    [HttpGet]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(typeof(List<StaffMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _service.ListAsync(ct);
        return Ok(list);
    }

    // PUT /api/staff-members/{staffMemberId}
    [HttpPut("{staffMemberId}")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(string staffMemberId, [FromBody] UpdateStaffMemberRequest request, CancellationToken ct)
    {
        var dto = await _service.UpdateAsync(staffMemberId, request, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }
}

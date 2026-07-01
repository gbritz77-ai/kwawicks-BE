using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/staff-members")]
[Produces("application/json")]
[Authorize(Policy = "AdminOnly")]
public class StaffMembersController : ControllerBase
{
    private readonly IStaffMemberService _service;
    private readonly IClientCreditService _creditService;

    public StaffMembersController(IStaffMemberService service, IClientCreditService creditService)
    {
        _service = service;
        _creditService = creditService;
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

    // POST /api/staff-members/{staffMemberId}/settle-deductions
    /// <summary>Master marks that a salary deduction has been processed for this staff member.
    /// Posts a SalaryDeduction credit entry equal to the outstanding negative balance, resetting the account to R0.</summary>
    [HttpPost("{staffMemberId}/settle-deductions")]
    [Authorize(Policy = "OwnerOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SettleDeductions(string staffMemberId, CancellationToken ct)
    {
        var staff = await _service.GetByIdAsync(staffMemberId, ct);
        if (staff is null) return NotFound(new { error = "Staff member not found." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var settled = await _creditService.SettleSalaryDeductionAsync(staffMemberId, userId, ct);

        return Ok(new
        {
            staffMemberId,
            staffName    = staff.Name,
            amountSettled = settled,
            newBalance   = 0m,
            message      = settled > 0
                ? $"R{settled:N2} salary deduction processed for {staff.Name}. Account reset to R0."
                : $"{staff.Name}'s account already has no outstanding balance."
        });
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

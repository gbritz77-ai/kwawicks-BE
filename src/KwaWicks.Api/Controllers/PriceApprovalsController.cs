using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/price-approvals")]
[Produces("application/json")]
[Authorize(Policy = "UserManagement")]
public class PriceApprovalsController : ControllerBase
{
    private readonly IPriceApprovalService _service;

    public PriceApprovalsController(IPriceApprovalService service)
        => _service = service;

    // GET /api/price-approvals
    [HttpGet]
    [ProducesResponseType(typeof(List<PriceApprovalResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await _service.GetPendingAsync(ct);
        return Ok(result);
    }

    // POST /api/price-approvals/{invoiceId}/approve
    [HttpPost("{invoiceId}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(string invoiceId, CancellationToken ct)
    {
        try
        {
            await _service.ApproveAsync(invoiceId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/price-approvals/{invoiceId}/amend
    [HttpPost("{invoiceId}/amend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AmendAndApprove(
        string invoiceId,
        [FromBody] AmendPriceRequest request,
        CancellationToken ct)
    {
        try
        {
            await _service.AmendAndApproveAsync(invoiceId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

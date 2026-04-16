using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/otp")]
public class OtpController : ControllerBase
{
    private readonly IOtpService _otp;

    public OtpController(IOtpService otp) => _otp = otp;

    // POST /api/otp/{invoiceId}/verify
    [HttpPost("{invoiceId}/verify")]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(typeof(OtpRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify(string invoiceId, [FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _otp.VerifyAsync(invoiceId, request.Code, User.Identity?.Name, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/otp/{invoiceId}/resend
    [HttpPost("{invoiceId}/resend")]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(typeof(OtpRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resend(string invoiceId, CancellationToken ct)
    {
        try
        {
            var result = await _otp.ResendAsync(invoiceId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/otp/{invoiceId}/bypass
    [HttpPost("{invoiceId}/bypass")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(OtpRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bypass(string invoiceId, [FromBody] BypassOtpRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _otp.BypassAsync(invoiceId, User.Identity?.Name, request.Reason, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/otp/report?clientId=&from=&to=
    [HttpGet("report")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<OtpRecordResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(
        [FromQuery] string? clientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var records = await _otp.GetReportAsync(clientId, from, to, ct);
        return Ok(records);
    }
}

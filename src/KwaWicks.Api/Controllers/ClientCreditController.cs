using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId}/credit")]
[Authorize(Policy = "FinancialAccess")]
public class ClientCreditController : ControllerBase
{
    private readonly IClientCreditService _service;

    public ClientCreditController(IClientCreditService service) => _service = service;

    // GET /api/clients/{clientId}/credit — full ledger with balance
    [HttpGet]
    public async Task<IActionResult> GetLedger(string clientId, CancellationToken ct)
    {
        var ledger = await _service.GetLedgerAsync(clientId, ct);
        return Ok(ledger);
    }

    // GET /api/clients/{clientId}/credit/balance — balance only (used at checkout)
    [HttpGet("balance")]
    [Authorize(Policy = "HubStaffOnly")]
    public async Task<IActionResult> GetBalance(string clientId, CancellationToken ct)
    {
        var balance = await _service.GetBalanceAsync(clientId, ct);
        return Ok(new { balance });
    }

    // GET /api/clients/{clientId}/credit/proof-upload-url?contentType=image/jpeg
    [HttpGet("proof-upload-url")]
    public async Task<IActionResult> GetProofUploadUrl(
        string clientId,
        [FromQuery] string contentType,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return BadRequest(new { error = "contentType is required." });

        var result = await _service.GetProofUploadUrlAsync(clientId, contentType, ct);
        return Ok(result);
    }

    // POST /api/clients/{clientId}/credit — add a deposit
    [HttpPost]
    public async Task<IActionResult> AddDeposit(
        string clientId,
        [FromBody] AddCreditDepositRequest request,
        CancellationToken ct)
    {
        try
        {
            // Capture who made the deposit from JWT
            request.CreatedByUserId =
                User.FindFirstValue("username") ??
                User.FindFirstValue(ClaimTypes.Name) ??
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                "unknown";

            var entry = await _service.AddDepositAsync(clientId, request, ct);
            return Ok(entry);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}

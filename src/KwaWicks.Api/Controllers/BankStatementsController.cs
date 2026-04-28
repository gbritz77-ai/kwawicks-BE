using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/bank-statements")]
[Produces("application/json")]
[Authorize(Policy = "FinancialAccess")]
public class BankStatementsController : ControllerBase
{
    private readonly IBankStatementService _service;

    public BankStatementsController(IBankStatementService service)
    {
        _service = service;
    }

    // GET /api/bank-statements/upload-url?fileName=mybank.csv
    [HttpGet("upload-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUploadUrl([FromQuery] string fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new { error = "fileName is required." });

        var (url, key) = await _service.GetUploadUrlAsync(fileName, ct);
        return Ok(new { uploadUrl = url, s3Key = key });
    }

    // POST /api/bank-statements  (after uploading to S3)
    [HttpPost]
    [ProducesResponseType(typeof(BankStatementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Process([FromBody] ProcessBankStatementRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.ProcessUploadAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { statementId = result.StatementId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/bank-statements
    [HttpGet]
    [ProducesResponseType(typeof(List<BankStatementSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _service.ListAsync(ct);
        return Ok(list);
    }

    // GET /api/bank-statements/{statementId}
    [HttpGet("{statementId}")]
    [ProducesResponseType(typeof(BankStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string statementId, CancellationToken ct)
    {
        var result = await _service.GetAsync(statementId, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // PUT /api/bank-statements/{statementId}/transactions/{transactionId}/allocate
    [HttpPut("{statementId}/transactions/{transactionId}/allocate")]
    [ProducesResponseType(typeof(BankStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Allocate(
        string statementId,
        string transactionId,
        [FromBody] AllocateBankTransactionRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.AllocateAsync(statementId, transactionId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    // DELETE /api/bank-statements/{statementId}/transactions/{transactionId}/allocate
    [HttpDelete("{statementId}/transactions/{transactionId}/allocate")]
    [ProducesResponseType(typeof(BankStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deallocate(
        string statementId,
        string transactionId,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.DeallocateAsync(statementId, transactionId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }
}

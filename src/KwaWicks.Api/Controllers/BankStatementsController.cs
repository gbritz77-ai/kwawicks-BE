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
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;

    public BankStatementsController(
        IBankStatementService service,
        IInvoiceService invoiceService,
        IClientService clientService)
    {
        _service        = service;
        _invoiceService = invoiceService;
        _clientService  = clientService;
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

    // POST /api/bank-statements
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

    // GET /api/bank-statements/{statementId}?search=&amount=
    [HttpGet("{statementId}")]
    [ProducesResponseType(typeof(BankStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        string statementId,
        [FromQuery] string? search,
        [FromQuery] decimal? amount,
        CancellationToken ct)
    {
        var result = await _service.GetAsync(statementId, ct, search, amount);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // GET /api/bank-statements/allocation-report?from=&to=
    [HttpGet("allocation-report")]
    [ProducesResponseType(typeof(List<BankReconAllocationReportItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AllocationReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _service.GetAllocationReportAsync(from, to, ct);
        return Ok(result);
    }

    // PUT /api/bank-statements/{statementId}/transactions/{transactionId}/allocate
    [HttpPut("{statementId}/transactions/{transactionId}/allocate")]
    [ProducesResponseType(typeof(AllocateResponse), StatusCodes.Status200OK)]
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

    // PUT /api/bank-statements/{statementId}/transactions/{transactionId}/allocate-non-client
    [HttpPut("{statementId}/transactions/{transactionId}/allocate-non-client")]
    [ProducesResponseType(typeof(AllocateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AllocateNonClient(
        string statementId,
        string transactionId,
        [FromBody] AllocateNonClientRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.AllocateNonClientAsync(statementId, transactionId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/bank-statements/{statementId}/transactions/{transactionId}/matches?search=
    // Returns pending, unreconciled invoices whose grand total matches the bank transaction amount.
    [HttpGet("{statementId}/transactions/{transactionId}/matches")]
    [ProducesResponseType(typeof(List<ReconInvoiceItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatches(
        string statementId,
        string transactionId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var statement = await _service.GetAsync(statementId, ct);
        if (statement is null) return NotFound(new { error = "Bank statement not found." });

        var tx = statement.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (tx is null) return NotFound(new { error = "Transaction not found or already allocated." });

        var matches = await _invoiceService.GetReconListAsync(
            paymentType: null,
            reconStatus: "pending",
            from: null,
            to: null,
            ct: ct,
            amount: tx.Amount);

        // Enrich with customer names before applying search
        var clients = await _clientService.ListAsync(1000, ct);
        var clientMap = clients.ToDictionary(c => c.ClientId, c => c.ClientName);
        foreach (var item in matches)
            item.CustomerName = clientMap.TryGetValue(item.CustomerId, out var name) ? name : item.CustomerId;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            matches = matches
                .Where(i =>
                    i.CustomerName.ToLowerInvariant().Contains(term) ||
                    i.InvoiceNumber.ToLowerInvariant().Contains(term))
                .ToList();
        }

        return Ok(matches);
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

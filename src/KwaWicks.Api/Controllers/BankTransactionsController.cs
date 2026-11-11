using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/bank-transactions")]
[Produces("application/json")]
[Authorize(Policy = "FinancialAccess")]
public class BankTransactionsController : ControllerBase
{
    private readonly IBankTransactionService _service;
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;

    public BankTransactionsController(
        IBankTransactionService service,
        IInvoiceService invoiceService,
        IClientService clientService)
    {
        _service        = service;
        _invoiceService = invoiceService;
        _clientService  = clientService;
    }

    // GET /api/bank-transactions/upload-url?fileName=
    [HttpGet("upload-url")]
    public async Task<IActionResult> GetUploadUrl([FromQuery] string fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new { error = "fileName is required." });
        var (url, key) = await _service.GetUploadUrlAsync(fileName, ct);
        return Ok(new { uploadUrl = url, s3Key = key });
    }

    // POST /api/bank-transactions/import
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ProcessBankStatementRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.ImportAsync(request.S3Key, request.FileName, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/bank-transactions?type=&status=&search=&from=&to=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _service.ListAsync(type, status, search, from, to, ct);
        return Ok(result);
    }

    // GET /api/bank-transactions/{txId}/matches?search=
    [HttpGet("{txId}/matches")]
    public async Task<IActionResult> GetMatches(
        string txId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        // We need the transaction amount to filter matches
        var list = await _service.ListAsync(null, null, null, null, null, ct);
        var tx = list.Pending.Concat(list.Reconned).FirstOrDefault(t => t.TransactionId == txId);
        if (tx is null) return NotFound(new { error = "Transaction not found." });

        var matches = await _invoiceService.GetReconListAsync(
            paymentType: null,
            reconStatus: null,
            from: null,
            to: null,
            ct: ct,
            amount: tx.Amount);

        var clients = await _clientService.ListAsync(1000, ct);
        var clientMap = clients.ToDictionary(c => c.ClientId, c => c.ClientName);
        foreach (var item in matches)
            item.CustomerName = clientMap.TryGetValue(item.CustomerId, out var name) ? name : item.CustomerId;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            matches = matches.Where(i =>
                i.CustomerName.ToLowerInvariant().Contains(term) ||
                i.InvoiceNumber.ToLowerInvariant().Contains(term)).ToList();
        }

        return Ok(matches);
    }

    // PUT /api/bank-transactions/{txId}/allocate
    [HttpPut("{txId}/allocate")]
    public async Task<IActionResult> Allocate(string txId, [FromBody] AllocateBankTransactionRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AllocateAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/allocate-non-client
    [HttpPut("{txId}/allocate-non-client")]
    public async Task<IActionResult> AllocateNonClient(string txId, [FromBody] AllocateNonClientRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AllocateNonClientAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/allocate-supplier
    [HttpPut("{txId}/allocate-supplier")]
    public async Task<IActionResult> AllocateSupplier(string txId, [FromBody] AllocateSupplierRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AllocateSupplierAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/allocate-client-credit
    [HttpPut("{txId}/allocate-client-credit")]
    public async Task<IActionResult> AllocateClientCredit(string txId, [FromBody] AllocateClientCreditRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AllocateClientCreditAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/split-client-credit
    [HttpPut("{txId}/split-client-credit")]
    public async Task<IActionResult> SplitClientCredit(string txId, [FromBody] SplitClientCreditRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.SplitClientCreditAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/allocate-expense
    [HttpPut("{txId}/allocate-expense")]
    public async Task<IActionResult> AllocateExpense(string txId, [FromBody] AllocateExpenseRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AllocateExpenseAsync(txId, request, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/bank-transactions/{txId}/allocate-petty-cash
    [HttpPut("{txId}/allocate-petty-cash")]
    public async Task<IActionResult> AllocatePettyCash(string txId, CancellationToken ct)
    {
        try { return Ok(await _service.AllocatePettyCashAsync(txId, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // DELETE /api/bank-transactions/{txId}/allocate
    [HttpDelete("{txId}/allocate")]
    public async Task<IActionResult> Deallocate(string txId, CancellationToken ct)
    {
        try { return Ok(await _service.DeallocateAsync(txId, ct)); }
        catch (InvalidOperationException ex) { return ex.Message.Contains("not found") ? NotFound(new { error = ex.Message }) : BadRequest(new { error = ex.Message }); }
    }

    // GET /api/bank-transactions/expense-categories
    [HttpGet("expense-categories")]
    public async Task<IActionResult> GetExpenseCategories(CancellationToken ct)
        => Ok(await _service.GetExpenseCategoriesAsync(ct));

    // POST /api/bank-transactions/expense-categories
    [HttpPost("expense-categories")]
    public async Task<IActionResult> AddExpenseCategory([FromBody] AddExpenseCategoryRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.AddExpenseCategoryAsync(request.Category, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/bank-transactions/migrate
    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate(CancellationToken ct)
    {
        var result = await _service.MigrateFromStatementsAsync(ct);
        return Ok(result);
    }

    // GET /api/bank-transactions/debit-report?from=&to=
    [HttpGet("debit-report")]
    public async Task<IActionResult> DebitReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _service.GetDebitReportAsync(from, to, ct));

    // GET /api/bank-transactions/allocation-report?from=&to=
    [HttpGet("allocation-report")]
    public async Task<IActionResult> AllocationReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _service.GetAllocationReportAsync(from, to, ct));
}

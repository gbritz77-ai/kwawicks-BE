using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize(Policy = "MasterOnly")]
[Produces("application/json")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _service;

    public BranchesController(IBranchService service) => _service = service;

    // GET /api/branches
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    // GET /api/branches/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        try { return Ok(await _service.GetAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // POST /api/branches
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(req, ct);
            return StatusCode(201, dto);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/branches/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateBranchRequest req, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateAsync(id, req, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // DELETE /api/branches/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // POST /api/branches/{id}/users
    [HttpPost("{id}/users")]
    public async Task<IActionResult> CreateUser(string id, [FromBody] CreateBranchUserRequest req, CancellationToken ct)
    {
        try { return StatusCode(201, await _service.CreateUserAsync(id, req, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/branches/{id}/users/{userId}/pin
    [HttpPut("{id}/users/{userId}/pin")]
    public async Task<IActionResult> UpdateUserPin(string id, string userId, [FromBody] UpdateBranchUserPinRequest req, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateUserPinAsync(id, userId, req, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // DELETE /api/branches/{id}/users/{userId}
    [HttpDelete("{id}/users/{userId}")]
    public async Task<IActionResult> DeleteUser(string id, string userId, CancellationToken ct)
    {
        try { await _service.DeleteUserAsync(id, userId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

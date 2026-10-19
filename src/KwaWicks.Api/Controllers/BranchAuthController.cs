using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/branch-auth")]
[AllowAnonymous]
[Produces("application/json")]
public class BranchAuthController : ControllerBase
{
    private readonly IBranchService _service;
    private readonly IConfiguration _config;

    public BranchAuthController(IBranchService service, IConfiguration config)
    {
        _service = service;
        _config  = config;
    }

    // POST /api/branch-auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] BranchLoginRequest req, CancellationToken ct)
    {
        try
        {
            var secret = _config["BranchJwt:Secret"]
                ?? throw new InvalidOperationException("BranchJwt:Secret not configured.");
            var result = await _service.LoginAsync(req, secret, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

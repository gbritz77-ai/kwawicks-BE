using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var username =
            User.FindFirst("cognito:username")?.Value ??
            User.FindFirst(ClaimTypes.Name)?.Value ??
            User.FindFirst("username")?.Value;

        var groups = User.FindAll("cognito:groups")
                         .Select(c => c.Value)
                         .ToList();

        return Ok(new
        {
            username,
            groups
        });
    }

}

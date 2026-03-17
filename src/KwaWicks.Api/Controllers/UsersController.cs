using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "UserManagement")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IConfiguration _config;

    public UsersController(IUserManagementService users, IAmazonCognitoIdentityProvider cognito, IConfiguration config)
    {
        _users = users;
        _cognito = cognito;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _users.ListUsersAsync(ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        try
        {
            var user = await _users.CreateUserAsync(req, ct);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{username}/pin")]
    public async Task<IActionResult> SetPin(string username, [FromBody] SetPinRequest req, CancellationToken ct)
    {
        try
        {
            await _users.SetPinAsync(username, req.Pin, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{username}/group")]
    public async Task<IActionResult> UpdateGroup(string username, [FromBody] KwaWicks.Application.DTOs.UpdateGroupRequest req, CancellationToken ct)
    {
        try
        {
            await _users.UpdateGroupAsync(username, req.NewGroup, req.OldGroup, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{username}")]
    public async Task<IActionResult> Delete(string username, CancellationToken ct)
    {
        var currentUser = User.FindFirstValue("cognito:username")
                       ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? "";
        if (string.Equals(username, currentUser, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "You cannot delete your own account." });

        try
        {
            await _users.DeleteUserAsync(username, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/users/drivers — used by DeliveryOrdersPage
    [HttpGet("drivers")]
    [Authorize(Policy = "HubStaffOnly")]
    public async Task<IActionResult> ListDrivers(CancellationToken ct)
    {
        var userPoolId = _config["Cognito:UserPoolId"]
            ?? throw new InvalidOperationException("Missing Cognito:UserPoolId");

        var drivers = new List<object>();
        string? nextToken = null;

        do
        {
            var req = new ListUsersInGroupRequest
            {
                UserPoolId = userPoolId,
                GroupName = "Driver",
                Limit = 60,
                NextToken = nextToken
            };

            var response = await _cognito.ListUsersInGroupAsync(req, ct);

            foreach (var user in response.Users)
            {
                var email = user.Attributes.FirstOrDefault(a => a.Name == "email")?.Value ?? "";
                var name = user.Attributes.FirstOrDefault(a => a.Name == "name")?.Value
                        ?? user.Attributes.FirstOrDefault(a => a.Name == "preferred_username")?.Value
                        ?? user.Username;

                drivers.Add(new
                {
                    userId = user.Username,
                    name,
                    email
                });
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return Ok(drivers);
    }
}

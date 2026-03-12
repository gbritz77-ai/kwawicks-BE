using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IConfiguration _config;

    public UsersController(IAmazonCognitoIdentityProvider cognito, IConfiguration config)
    {
        _cognito = cognito;
        _config = config;
    }

    // GET /api/users/drivers
    [HttpGet("drivers")]
    [Authorize(Policy = "HubStaffOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

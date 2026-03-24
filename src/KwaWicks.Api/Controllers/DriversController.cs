using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/drivers")]
[Authorize(Policy = "HubStaffOnly")]
public class DriversController : ControllerBase
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IConfiguration _config;

    public DriversController(IAmazonCognitoIdentityProvider cognito, IConfiguration config)
    {
        _cognito = cognito;
        _config = config;
    }

    // GET /api/drivers — list all users in the Driver Cognito group
    [HttpGet]
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
                var name = user.Attributes.FirstOrDefault(a => a.Name == "name")?.Value
                        ?? user.Attributes.FirstOrDefault(a => a.Name == "preferred_username")?.Value
                        ?? user.Username;

                drivers.Add(new { userId = user.Username, name, email = user.Attributes.FirstOrDefault(a => a.Name == "email")?.Value ?? "" });
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return Ok(drivers);
    }
}

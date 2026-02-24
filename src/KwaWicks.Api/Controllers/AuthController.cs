using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Mvc;
using KwaWicks.Api.Contracts.Auth;

namespace KwaWicks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAmazonCognitoIdentityProvider cognito,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _cognito = cognito;
        _config = config;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        // IMPORTANT: matches your current DTO property names
        var username = req.UsernameOrEmail?.Trim();
        var pin = req.Password?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(pin))
            return BadRequest(new { error = "VALIDATION", message = "Username and PIN are required." });

        if (!IsValidSixDigitPin(pin))
            return BadRequest(new { error = "VALIDATION", message = "PIN must be exactly 6 digits." });

        var clientId = _config["Cognito:AppClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogError("Cognito AppClientId is missing in configuration (Cognito:AppClientId).");
            return StatusCode(500, new { error = "CONFIG", message = "Authentication is not configured." });
        }

        try
        {
            var response = await _cognito.InitiateAuthAsync(new InitiateAuthRequest
            {
                ClientId = clientId,
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    ["USERNAME"] = username,
                    ["PASSWORD"] = pin
                }
            }, ct);

            if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
            {
                // Keep UX simple: tell UI to prompt for new PIN.
                return Unauthorized(new
                {
                    error = "NEW_PASSWORD_REQUIRED",
                    message = "You must set a new PIN before you can sign in."
                    // If you later implement this flow, also return: session = response.Session
                });
            }

            var result = response.AuthenticationResult;
            if (result == null)
                return Unauthorized(new { error = "AUTH_FAILED", message = "Login failed." });

            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken ?? "",
                IdToken = result.IdToken ?? "",
                RefreshToken = result.RefreshToken, // nullable in your DTO
                ExpiresIn = result.ExpiresIn ?? 0,
                TokenType = string.IsNullOrWhiteSpace(result.TokenType) ? "Bearer" : result.TokenType
            });
        }
        catch (NotAuthorizedException)
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Incorrect username or PIN." });
        }
        catch (UserNotConfirmedException)
        {
            return Unauthorized(new { error = "USER_NOT_CONFIRMED", message = "User is not confirmed." });
        }
        catch (UserNotFoundException)
        {
            return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Incorrect username or PIN." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for username {Username}.", username);
            return StatusCode(500, new { error = "AUTH_FAILED", message = "Something went wrong. Please try again." });
        }
    }
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { error = "VALIDATION", message = "Refresh token is required." });

        var clientId = _config["Cognito:AppClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(500, new { error = "CONFIG", message = "Authentication is not configured." });

        try
        {
            var response = await _cognito.InitiateAuthAsync(new InitiateAuthRequest
            {
                ClientId = clientId,
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    ["REFRESH_TOKEN"] = req.RefreshToken
                }
            }, ct);

            var result = response.AuthenticationResult;
            if (result == null)
                return Unauthorized(new { error = "AUTH_FAILED", message = "Refresh failed." });

            // Cognito may NOT return a new refresh token here; keep existing on UI
            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken ?? "",
                IdToken = result.IdToken ?? "",
                RefreshToken = null,
                ExpiresIn = result.ExpiresIn ?? 0,
                TokenType = result.TokenType ?? "Bearer"
            });
        }
        catch (NotAuthorizedException)
        {
            return Unauthorized(new { error = "INVALID_REFRESH", message = "Session expired. Please sign in again." });
        }
    }

    private static bool IsValidSixDigitPin(string pin)
    {
        if (pin.Length != 6) return false;
        foreach (var ch in pin)
            if (!char.IsDigit(ch))
                return false;
        return true;
    }
}
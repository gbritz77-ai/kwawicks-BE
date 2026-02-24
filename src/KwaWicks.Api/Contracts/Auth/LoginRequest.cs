using System.Text.Json.Serialization;

namespace KwaWicks.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [JsonPropertyName("usernameOrEmail")]
    public string UsernameOrEmail { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}
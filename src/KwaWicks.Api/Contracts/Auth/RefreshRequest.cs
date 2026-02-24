using System.Text.Json.Serialization;

namespace KwaWicks.Api.Contracts.Auth;

public sealed class RefreshRequest
{
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = "";
}
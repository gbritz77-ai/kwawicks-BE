namespace KwaWicks.Api.Contracts.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string IdToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}

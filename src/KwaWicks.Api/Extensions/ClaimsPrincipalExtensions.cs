using System.Security.Claims;

namespace KwaWicks.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the hub_id claim value when the caller is a branch user, otherwise null.
    /// Cognito users (Master/Owner/Admin etc.) return null and see all data.
    /// </summary>
    public static string? GetHubId(this ClaimsPrincipal user)
        => user.FindFirstValue("hub_id");
}

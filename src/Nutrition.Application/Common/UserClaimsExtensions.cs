using System.Security.Claims;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Cryptographic claims extraction helpers for authenticated users.
/// Eliminates insecure client-supplied identity parameters (OWASP A01: Broken Access Control).
/// </summary>
public static class UserClaimsExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("userId")?.Value;
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value;
    }

    public static string? GetTier(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("tier")?.Value;
    }

    public static bool IsAdminOrSuper(this ClaimsPrincipal principal)
    {
        var role = principal.GetRole();
        return role is nameof(UserRole.Admin) or nameof(UserRole.SuperAdmin);
    }
}

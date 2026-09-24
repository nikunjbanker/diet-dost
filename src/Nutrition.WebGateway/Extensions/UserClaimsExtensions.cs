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
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("userId");
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
    }

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");
    }

    public static string? GetTier(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("tier");
    }

    public static bool IsAdminOrSuper(this ClaimsPrincipal principal)
    {
        var role = principal.GetRole();
        return role is nameof(UserRole.Admin) or nameof(UserRole.SuperAdmin);
    }
}

using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Port abstraction for accessing the ambient authenticated user context,
/// shielding Application layer handlers from ASP.NET Core HttpContext / ClaimsPrincipal coupling.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    UserRole Role { get; }
    UserTier Tier { get; }
    bool IsAuthenticated { get; }
    bool IsAdminOrSuper { get; }
}

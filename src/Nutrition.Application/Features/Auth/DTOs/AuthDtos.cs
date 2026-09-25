using System.Security.Claims;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.DTOs;

public record RegisterResultDto(
    string UserId,
    string Email,
    string Message,
    string? DevOtpCode
);

public record CurrentUserResponseDto(
    string Id,
    string Email,
    string MobileNumber,
    string Name,
    UserRole Role,
    UserTier Tier,
    bool IsEmailVerified,
    bool IsMobileVerified,
    TierFeatureConfiguration? Entitlements
);

public record LoginResultDto(
    string Token,
    CurrentUserResponseDto User,
    ClaimsPrincipal Principal
);

public record VerifyOtpResultDto(
    string Message,
    string? Token,
    CurrentUserResponseDto? User,
    ClaimsPrincipal? Principal
);

public record ForgotPasswordResultDto(
    string Message,
    string? DevOtpCode
);

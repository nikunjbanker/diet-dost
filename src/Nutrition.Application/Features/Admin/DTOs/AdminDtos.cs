using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Admin.DTOs;

public record AdminUserSummaryDto(
    string Id,
    string Email,
    string MobileNumber,
    UserRole Role,
    UserTier Tier,
    bool IsEmailVerified,
    bool IsMobileVerified,
    bool IsActive,
    DateTime? TermsAcceptedAtUtc,
    string? TermsVersionAccepted,
    DateTime? HealthConsentAcceptedAtUtc,
    string? HealthConsentVersionAccepted,
    string? ConsentIpAddress,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    int TodayAiDetectionsCount
);

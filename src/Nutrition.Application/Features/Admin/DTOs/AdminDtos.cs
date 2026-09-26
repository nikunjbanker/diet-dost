/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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

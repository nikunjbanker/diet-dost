/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Features.Admin.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.DTOs;

/// <summary>
/// Persistence- and framework-free claims payload returned by Auth CQRS handlers
/// and materialized into ClaimsPrincipal by the Presentation layer.
/// </summary>
public sealed record AuthenticatedClaimsPayload(
    string UserId,
    string DisplayName,
    string Email,
    string Role,
    string Tier,
    string SecurityStamp,
    bool IsAnonymous = false,
    string AuthenticationScheme = "SmartScheme"
);

public record RegisterResultDto(
    string UserId,
    string Email,
    string Message,
    string? DevOtpCode,
    bool RequiresOwnerApproval = true,
    string? RegistrationTicket = null
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
    TierFeatureConfigurationDto? Entitlements,
    bool IsOnboarded = false,
    bool IsAnonymous = false
);

public record LoginResultDto(
    string Token,
    CurrentUserResponseDto User,
    AuthenticatedClaimsPayload Claims
);

public record VerifyOtpResultDto(
    string Message,
    string? Token,
    CurrentUserResponseDto? User,
    AuthenticatedClaimsPayload? Claims
);

public record ForgotPasswordResultDto(
    string Message,
    string? DevOtpCode
);

public record RegisterAnonymousResultDto(
    string UserId,
    string DisplayName,
    string Token,
    string RecoveryKeyHex,
    string MnemonicPhrase,
    string RecoveryLookupId,
    string JsonBackupPayload,
    CurrentUserResponseDto User,
    AuthenticatedClaimsPayload Claims
);

public record InitiateQrSessionResultDto(
    string SessionId,
    string ChallengeNonce,
    string ConfirmationCode,
    string ConfirmationGlyph,
    string QrPayloadUri,
    DateTime ExpiresAtUtc,
    int TtlSeconds,
    string? DeepLinkUri = null,
    string? QrSvg = null,
    string? DataUri = null
);

public record QrSessionStatusDto(
    string SessionId,
    QrSessionStatus Status,
    bool IsExpired,
    string? ExchangeTicket,
    string? MobilePublicKey,
    string? EncryptedApprovalPayload,
    string? ClientIpAddress,
    string? ClientUserAgent
);

public record ExchangeQrTicketResultDto(
    string Token,
    CurrentUserResponseDto User,
    AuthenticatedClaimsPayload Claims
);

public record RecoverAccountResultDto(
    string Token,
    CurrentUserResponseDto User,
    AuthenticatedClaimsPayload Claims,
    string NewRecoveryKeyHex,
    string NewMnemonicPhrase,
    string NewRecoveryLookupId,
    string NewJsonBackupPayload
);

public record RecoveryKitIssueDto(
    string RecoveryKitIssueId,
    string RecoveryKeyHex,
    string MnemonicPhrase,
    string RecoveryLookupId,
    string JsonBackupPayload
);

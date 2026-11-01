/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.DTOs;

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
    TierFeatureConfiguration? Entitlements,
    bool IsOnboarded = false
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

public record RegisterAnonymousResultDto(
    string UserId,
    string DisplayName,
    string Token,
    string RecoveryKeyHex,
    string MnemonicPhrase,
    string RecoveryLookupId,
    string JsonBackupPayload,
    CurrentUserResponseDto User,
    ClaimsPrincipal Principal
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
    ClaimsPrincipal Principal
);

public record RecoverAccountResultDto(
    string Token,
    CurrentUserResponseDto User,
    ClaimsPrincipal Principal,
    string NewRecoveryKeyHex,
    string NewMnemonicPhrase,
    string NewRecoveryLookupId,
    string NewJsonBackupPayload
);

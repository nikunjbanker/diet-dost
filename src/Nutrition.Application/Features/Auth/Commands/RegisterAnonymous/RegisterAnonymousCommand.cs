/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Common.Security;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.RegisterAnonymous;

public record RegisterAnonymousCommand(
    string? DisplayName,
    bool AcceptTerms,
    bool AcceptHealthConsent,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<Result<RegisterAnonymousResultDto>>;

public class RegisterAnonymousCommandHandler : ICommandHandler<RegisterAnonymousCommand, Result<RegisterAnonymousResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<RegisterAnonymousCommandHandler> _logger;

    public RegisterAnonymousCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork uow,
        IJwtTokenService jwtTokenService,
        IOptions<AuthOptions> authOptions,
        ILogger<RegisterAnonymousCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _tierRepo = tierRepo;
        _uow = uow;
        _jwtTokenService = jwtTokenService;
        _authOptions = authOptions?.Value ?? new AuthOptions();
        _logger = logger;
    }

    public async Task<Result<RegisterAnonymousResultDto>> HandleAsync(RegisterAnonymousCommand request, CancellationToken ct = default)
    {
        if (!request.AcceptTerms)
        {
            return Result<RegisterAnonymousResultDto>.Failure(
                "You must accept the Terms of Service & AI Training License under India DPDPA 2023.",
                "TermsNotAccepted",
                400);
        }

        if (!request.AcceptHealthConsent)
        {
            return Result<RegisterAnonymousResultDto>.Failure(
                "Explicit consent for Clinical Health & Nutrition Processing is required under Section 6 of DPDPA 2023.",
                "HealthConsentNotAccepted",
                400);
        }

        var userId = "usr_" + Guid.NewGuid().ToString("N")[..12];
        var sanitizedUserId = PhotoPathSanitizer.SanitizeUserId(userId);
        var effectiveName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Anonymous Friend" : request.DisplayName.Trim();
        var syntheticEmail = $"anon_{sanitizedUserId}@dietdost.local";

        var recoveryKit = RecoveryKit.GenerateNew(effectiveName, sanitizedUserId);

        var user = new ApplicationUser
        {
            Id = sanitizedUserId,
            Email = syntheticEmail,
            NormalizedEmail = syntheticEmail.ToUpperInvariant(),
            MobileNumber = "ANONYMOUS",
            NormalizedMobileNumber = "ANONYMOUS",
            PasswordHash = "ANONYMOUS_NO_PASSWORD",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true, // Anonymous accounts are pre-verified via recovery kit
            IsMobileVerified = false,
            IsActive = true,
            IsAnonymous = true,
            DisplayName = effectiveName,
            RecoveryLookupId = recoveryKit.RecoveryLookupId,
            RecoveryKeyHash = recoveryKit.RecoveryKeyHash,
            RecoverySalt = recoveryKit.RecoverySalt,
            RecoveryKeyCreatedAtUtc = DateTime.UtcNow,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609",
            ConsentIpAddress = request.IpAddress ?? "Unknown",
            ConsentUserAgent = request.UserAgent ?? "Unknown",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Invariant guard
        user.ValidateRegistration();

        await _userRepo.AddAsync(user, ct);

        var profile = new UserProfile
        {
            Id = sanitizedUserId,
            Name = effectiveName,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await _profileRepo.AddAsync(profile, ct);

        await _uow.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user, effectiveName);
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, effectiveName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tier", user.Tier.ToString()),
            new("security_stamp", user.SecurityStamp),
            new("is_anonymous", "true")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "SmartScheme"));

        var responseUser = new CurrentUserResponseDto(
            user.Id,
            user.Email,
            user.MobileNumber,
            effectiveName,
            user.Role,
            user.Tier,
            user.IsEmailVerified,
            user.IsMobileVerified,
            tierConfig
        );

        _logger.LogInformation("[AUTH] Successfully registered anonymous user {UserId} with O(1) RecoveryLookupId {LookupId}", user.Id, user.RecoveryLookupId);

        return Result<RegisterAnonymousResultDto>.Success(new RegisterAnonymousResultDto(
            user.Id,
            effectiveName,
            token,
            recoveryKit.MasterEntropyHex,
            recoveryKit.MnemonicPhrase,
            recoveryKit.RecoveryLookupId,
            recoveryKit.JsonBackupPayload,
            responseUser,
            principal
        ), 201);
    }
}

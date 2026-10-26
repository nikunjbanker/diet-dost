/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.RecoverAccount;

public record RecoverAccountCommand(
    string RecoveryKeyOrMnemonic
) : ICommand<Result<RecoverAccountResultDto>>;

public class RecoverAccountCommandHandler : ICommandHandler<RecoverAccountCommand, Result<RecoverAccountResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<RecoverAccountCommandHandler> _logger;

    public RecoverAccountCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork uow,
        IJwtTokenService jwtTokenService,
        ILogger<RecoverAccountCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _tierRepo = tierRepo;
        _uow = uow;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<RecoverAccountResultDto>> HandleAsync(RecoverAccountCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecoveryKeyOrMnemonic))
        {
            return Result<RecoverAccountResultDto>.Failure("Recovery key or 12-word mnemonic phrase is required.", "MissingRecoveryInput", 400);
        }

        var input = request.RecoveryKeyOrMnemonic.Trim();
        byte[] entropy;

        // Check if 12-word mnemonic
        if (input.Contains(' '))
        {
            if (!Bip39Mnemonic.ValidateMnemonic(input, out entropy))
            {
                return Result<RecoverAccountResultDto>.Failure("Invalid 12-word BIP-39 mnemonic phrase or checksum failure.", "InvalidMnemonic", 401);
            }
        }
        else
        {
            // Hex key
            try
            {
                entropy = Convert.FromHexString(input);
                if (entropy.Length != 16)
                {
                    return Result<RecoverAccountResultDto>.Failure("Recovery key must represent exactly 16 bytes (128 bits) of entropy.", "InvalidKeyLength", 401);
                }
            }
            catch (FormatException)
            {
                return Result<RecoverAccountResultDto>.Failure("Invalid recovery key format.", "InvalidKeyFormat", 401);
            }
        }

        // O(1) HKDF Lookup
        var lookupId = RecoveryKit.DeriveLookupId(entropy);
        var user = await _userRepo.FirstOrDefaultAsync(u => u.RecoveryLookupId == lookupId, ct);

        if (user == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[DISASTER-RECOVERY] Recovery attempt with unmapped lookup ID: {LookupId}", lookupId);
            }
            return Result<RecoverAccountResultDto>.Failure("No active account matches the provided emergency recovery credentials.", "AccountNotFound", 401);
        }

        if (user.IsDeleted)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[DISASTER-RECOVERY] Recovery attempted on soft-deleted account {UserId}", user.Id);
            }
            return Result<RecoverAccountResultDto>.Failure(
                "This account was permanently destroyed pursuant to India DPDPA 2023 Section 12 Right to Erasure and cannot be recovered.",
                "AccountPermanentlyDeleted",
                410);
        }

        if (!user.IsActive)
        {
            return Result<RecoverAccountResultDto>.Failure("This account is currently locked or suspended.", "AccountDeactivated", 403);
        }

        if (string.IsNullOrWhiteSpace(user.RecoverySalt) || string.IsNullOrWhiteSpace(user.RecoveryKeyHash))
        {
            return Result<RecoverAccountResultDto>.Failure("Corrupted account recovery security record. Please contact support.", "RecoveryRecordCorrupted", 500);
        }

        var salt = Convert.FromHexString(user.RecoverySalt);
        if (!RecoveryKit.VerifyKey(entropy, salt, user.RecoveryKeyHash))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[DISASTER-RECOVERY] Constant-time PBKDF2 hash verification failed for user {UserId}", user.Id);
            }
            return Result<RecoverAccountResultDto>.Failure("Invalid recovery credentials.", "InvalidCredentials", 401);
        }

        // 1. Invalidate all previous sessions by rotating SecurityStamp
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.LastLoginAtUtc = DateTime.UtcNow;

        // 2. Mandatory Key Rotation: Issue brand new recovery key (prevents reuse of old backup file)
        var newKit = RecoveryKit.GenerateNew(user.DisplayName, user.Id);
        user.RecoveryLookupId = newKit.RecoveryLookupId;
        user.RecoveryKeyHash = newKit.RecoveryKeyHash;
        user.RecoverySalt = newKit.RecoverySalt;
        user.RecoveryKeyCreatedAtUtc = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);

        var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        var displayName = profile?.Name ?? user.DisplayName ?? user.Email;

        var token = _jwtTokenService.GenerateToken(user, displayName);
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tier", user.Tier.ToString()),
            new("security_stamp", user.SecurityStamp)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "SmartScheme"));

        var responseUser = new CurrentUserResponseDto(
            user.Id,
            user.Email,
            user.MobileNumber,
            displayName,
            user.Role,
            user.Tier,
            user.IsEmailVerified,
            user.IsMobileVerified,
            tierConfig
        );

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[DISASTER-RECOVERY] Successfully recovered account {UserId} with rotated recovery key and security stamp", user.Id);
        }

        return Result<RecoverAccountResultDto>.Success(new RecoverAccountResultDto(
            token,
            responseUser,
            principal,
            newKit.MasterEntropyHex,
            newKit.MnemonicPhrase,
            newKit.RecoveryLookupId,
            newKit.JsonBackupPayload
        ));
    }
}

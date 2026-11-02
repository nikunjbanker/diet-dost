/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.GenerateRecoveryKit;

public record GenerateRecoveryKitCommand(string UserId, string CurrentPassword)
    : ICommand<Result<RecoveryKitIssueDto>>;

public class GenerateRecoveryKitCommandHandler
    : ICommandHandler<GenerateRecoveryKitCommand, Result<RecoveryKitIssueDto>>
{
    private static readonly TimeSpan IssueLifetime = TimeSpan.FromMinutes(15);
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GenerateRecoveryKitCommandHandler> _logger;

    public GenerateRecoveryKitCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IPasswordHasher passwordHasher,
        IUnitOfWork uow,
        ILogger<GenerateRecoveryKitCommandHandler> logger)
    {
        _userRepo = userRepo;
        _passwordHasher = passwordHasher;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<RecoveryKitIssueDto>> HandleAsync(
        GenerateRecoveryKitCommand request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return Result<RecoveryKitIssueDto>.Failure(
                "Password verification failed.", "PasswordVerificationFailed", 401);
        }

        var user = await _userRepo.GetByIdAsync(request.UserId, ct);
        if (user is null || user.IsDeleted || !user.IsActive || user.IsAnonymous ||
            !_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return Result<RecoveryKitIssueDto>.Failure(
                "Password verification failed.", "PasswordVerificationFailed", 401);
        }

        var now = DateTime.UtcNow;
        var kit = RecoveryKit.GenerateNew(user.DisplayName, user.Id);
        var issueId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        user.PendingRecoveryLookupId = kit.RecoveryLookupId;
        user.PendingRecoveryKeyHash = kit.RecoveryKeyHash;
        user.PendingRecoverySalt = kit.RecoverySalt;
        user.PendingRecoveryKeyCreatedAtUtc = now;
        user.PendingRecoveryKeyExpiresAtUtc = now.Add(IssueLifetime);
        user.PendingRecoveryIssueTokenHash = RecoveryKitIssueToken.ComputeHash(issueId);
        user.LastActivatedRecoveryIssueTokenHash = null;

        await _uow.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Staged a recovery kit for user {UserId}; activation is pending user confirmation.", user.Id);
        }

        return Result<RecoveryKitIssueDto>.Success(new RecoveryKitIssueDto(
            issueId,
            kit.MasterEntropyHex,
            kit.MnemonicPhrase,
            kit.RecoveryLookupId,
            kit.JsonBackupPayload));
    }
}

internal static class RecoveryKitIssueToken
{
    public static string ComputeHash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool Matches(string? storedHash, string token)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || token.Length != 64)
        {
            return false;
        }

        try
        {
            var storedBytes = Convert.FromHexString(storedHash);
            var suppliedBytes = Convert.FromHexString(ComputeHash(token));
            return storedBytes.Length == 32 &&
                   CryptographicOperations.FixedTimeEquals(storedBytes, suppliedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

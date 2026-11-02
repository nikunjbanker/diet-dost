/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.Commands.GenerateRecoveryKit;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.ActivateRecoveryKit;

public record ActivateRecoveryKitCommand(string UserId, string RecoveryKitIssueId)
    : ICommand<Result<string>>;

public class ActivateRecoveryKitCommandHandler
    : ICommandHandler<ActivateRecoveryKitCommand, Result<string>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ActivateRecoveryKitCommandHandler> _logger;

    public ActivateRecoveryKitCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<ActivateRecoveryKitCommandHandler> logger)
    {
        _userRepo = userRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<string>> HandleAsync(
        ActivateRecoveryKitCommand request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.RecoveryKitIssueId))
        {
            return Result<string>.Failure(
                "Recovery kit confirmation is no longer available.", "RecoveryKitIssueUnavailable", 409);
        }

        var user = await _userRepo.GetByIdAsync(request.UserId, ct);
        if (user is null || !user.IsActive || user.IsAnonymous)
        {
            return Result<string>.Failure(
                "Recovery kit confirmation is no longer available.", "RecoveryKitIssueUnavailable", 409);
        }

        if (RecoveryKitIssueToken.Matches(
                user.LastActivatedRecoveryIssueTokenHash,
                request.RecoveryKitIssueId))
        {
            return Result<string>.Success("Recovery kit activated.");
        }

        if (!RecoveryKitIssueToken.Matches(
                user.PendingRecoveryIssueTokenHash,
                request.RecoveryKitIssueId))
        {
            return Result<string>.Failure(
                "Recovery kit confirmation is no longer available.", "RecoveryKitIssueUnavailable", 409);
        }

        if (user.PendingRecoveryKeyExpiresAtUtc is not DateTime expiresAtUtc ||
            expiresAtUtc <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(user.PendingRecoveryLookupId) ||
            string.IsNullOrWhiteSpace(user.PendingRecoveryKeyHash) ||
            string.IsNullOrWhiteSpace(user.PendingRecoverySalt) ||
            user.PendingRecoveryKeyCreatedAtUtc is not DateTime createdAtUtc)
        {
            ClearPendingIssue(user);
            await _uow.SaveChangesAsync(ct);
            return Result<string>.Failure(
                "Recovery kit confirmation is no longer available.", "RecoveryKitIssueUnavailable", 409);
        }

        user.RecoveryLookupId = user.PendingRecoveryLookupId;
        user.RecoveryKeyHash = user.PendingRecoveryKeyHash;
        user.RecoverySalt = user.PendingRecoverySalt;
        user.RecoveryKeyCreatedAtUtc = createdAtUtc;
        user.LastActivatedRecoveryIssueTokenHash = user.PendingRecoveryIssueTokenHash;
        ClearPendingIssue(user);

        await _uow.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Activated a user-confirmed recovery kit for user {UserId}.", user.Id);
        }

        return Result<string>.Success("Recovery kit activated.");
    }

    private static void ClearPendingIssue(ApplicationUser user)
    {
        user.PendingRecoveryLookupId = null;
        user.PendingRecoveryKeyHash = null;
        user.PendingRecoverySalt = null;
        user.PendingRecoveryKeyCreatedAtUtc = null;
        user.PendingRecoveryKeyExpiresAtUtc = null;
        user.PendingRecoveryIssueTokenHash = null;
    }
}

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
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.SimulateQrApproval;

public record SimulateQrApprovalCommand(
    string SessionId,
    string ChallengeNonce,
    string? DemoEmail = null
) : ICommand<Result<string>>;

public class SimulateQrApprovalCommandHandler : ICommandHandler<SimulateQrApprovalCommand, Result<string>>
{
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogger<SimulateQrApprovalCommandHandler> _logger;

    public SimulateQrApprovalCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        IAppEnvironment? appEnvironment,
        ILogger<SimulateQrApprovalCommandHandler> logger)
    {
        _qrRepo = qrRepo;
        _userRepo = userRepo;
        _uow = uow;
        _appEnvironment = appEnvironment ?? new FallbackEnvironment();
        _logger = logger;
    }

    public SimulateQrApprovalCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<SimulateQrApprovalCommandHandler> logger)
        : this(qrRepo, userRepo, uow, null, logger)
    {
    }

    private sealed class FallbackEnvironment : IAppEnvironment
    {
        public bool IsDebugMode => true;
        public bool IsDevelopment => true;
        public bool AllowsDemoUsers => true;
        public bool AllowsAdminDemoUsers => true;
    }

    public async Task<Result<string>> HandleAsync(SimulateQrApprovalCommand request, CancellationToken ct = default)
    {
        // GAP-39: Simulated mobile approval is restricted strictly to non-production environments with demo users enabled
        if (!_appEnvironment.AllowsDemoUsers)
        {
            return Result<string>.Failure(
                "Simulated mobile approval is permanently disabled in production environments.",
                "DemoSimulationDisabled",
                403);
        }

        var session = await _qrRepo.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session == null)
        {
            return Result<string>.Failure("QR Session not found.", "QrSessionNotFound", 404);
        }

        if (session.IsExpired)
        {
            return Result<string>.Failure("The QR code has expired. Please refresh your screen and scan the new code.", "QrSessionExpired", 410);
        }

        if (!string.Equals(session.ChallengeNonce, request.ChallengeNonce, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure("Cryptographic challenge nonce mismatch. Unauthorized approval attempt.", "ChallengeNonceMismatch", 400);
        }

        var targetEmail = !string.IsNullOrWhiteSpace(request.DemoEmail) ? request.DemoEmail.Trim() : "basic@dietdost.app";

        // GAP-39: Security Guard - Strictly prohibit simulated approval of root SuperAdmin or Admin accounts
        if (targetEmail.Equals("superadmin@dietdost.app", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(
                "Simulated approval of administrative accounts is strictly prohibited.",
                "AdminApprovalProhibited",
                400);
        }

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Email == targetEmail, ct);
        if (user == null && targetEmail.Equals("basic@dietdost.app", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback to any active non-admin demo user
            user = await _userRepo.FirstOrDefaultAsync(u => u.IsActive && !u.IsDeleted && u.Role != UserRole.SuperAdmin && u.Role != UserRole.Admin, ct);
        }

        if (user == null)
        {
            return Result<string>.Failure("No suitable user account found to approve demo session.", "UserNotFound", 404);
        }

        if (user.Role == UserRole.SuperAdmin || user.Role == UserRole.Admin)
        {
            return Result<string>.Failure(
                "Simulated approval of administrative accounts is strictly prohibited.",
                "AdminApprovalProhibited",
                400);
        }

        try
        {
            var exchangeTicket = session.Approve(user.Id);
            await _uow.SaveChangesAsync(ct);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[QR-AUTH] Simulated approval for QR session {SessionId} using user {UserEmail} ({UserId})",
                    session.Id, user.Email, user.Id);
            }

            return Result<string>.Success(exchangeTicket);
        }
        catch (InvalidOperationException ex)
        {
            return Result<string>.Failure(ex.Message, "InvalidSessionState", 409);
        }
    }
}

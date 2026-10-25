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
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.ApproveQrSession;

public record ApproveQrSessionCommand(
    string SessionId,
    string ChallengeNonce,
    string UserId,
    string? MobilePublicKey = null,
    string? EncryptedApprovalPayload = null
) : ICommand<Result<string>>;

public class ApproveQrSessionCommandHandler : ICommandHandler<ApproveQrSessionCommand, Result<string>>
{
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ApproveQrSessionCommandHandler> _logger;

    public ApproveQrSessionCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<ApproveQrSessionCommandHandler> logger)
    {
        _qrRepo = qrRepo;
        _userRepo = userRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<string>> HandleAsync(ApproveQrSessionCommand request, CancellationToken ct = default)
    {
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

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user == null || !user.IsActive)
        {
            return Result<string>.Failure("The approving user account is inactive or not found.", "UserInactive", 403);
        }

        if (user.IsDeleted)
        {
            return Result<string>.Failure("The approving user account has been deleted.", "UserDeleted", 410);
        }

        try
        {
            var exchangeTicket = session.Approve(user.Id, request.MobilePublicKey, request.EncryptedApprovalPayload);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("[QR-AUTH] User {UserId} successfully approved QR session {SessionId}", user.Id, session.Id);

            return Result<string>.Success(exchangeTicket);
        }
        catch (InvalidOperationException ex)
        {
            return Result<string>.Failure(ex.Message, "InvalidSessionState", 409);
        }
    }
}

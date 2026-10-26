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

namespace Nutrition.Application.Features.Auth.Commands.RejectQrSession;

public record RejectQrSessionCommand(
    string SessionId,
    string ChallengeNonce
) : ICommand<Result>;

public class RejectQrSessionCommandHandler : ICommandHandler<RejectQrSessionCommand, Result>
{
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RejectQrSessionCommandHandler> _logger;

    public RejectQrSessionCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IUnitOfWork uow,
        ILogger<RejectQrSessionCommandHandler> logger)
    {
        _qrRepo = qrRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(RejectQrSessionCommand request, CancellationToken ct = default)
    {
        var session = await _qrRepo.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session == null)
        {
            return Result.Failure("QR Session not found.", "QrSessionNotFound", 404);
        }

        if (session.IsExpired)
        {
            return Result.Failure("The QR code has expired.", "QrSessionExpired", 410);
        }

        if (!string.Equals(session.ChallengeNonce, request.ChallengeNonce, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Cryptographic challenge nonce mismatch.", "ChallengeNonceMismatch", 400);
        }

        try
        {
            session.Reject();
            await _uow.SaveChangesAsync(ct);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[QR-AUTH] QR session {SessionId} rejected", session.Id);
            }

            return Result.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message, "InvalidSessionState", 409);
        }
    }
}

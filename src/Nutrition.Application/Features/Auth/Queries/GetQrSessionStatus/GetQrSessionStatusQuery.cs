/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Queries.GetQrSessionStatus;

public record GetQrSessionStatusQuery(string SessionId) : IQuery<Result<QrSessionStatusDto>>;

public class GetQrSessionStatusQueryHandler : IQueryHandler<GetQrSessionStatusQuery, Result<QrSessionStatusDto>>
{
    private readonly IRepository<QrLoginSession> _qrRepo;

    public GetQrSessionStatusQueryHandler(IRepository<QrLoginSession> qrRepo)
    {
        _qrRepo = qrRepo;
    }

    public async Task<Result<QrSessionStatusDto>> HandleAsync(GetQrSessionStatusQuery request, CancellationToken ct = default)
    {
        var session = await _qrRepo.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session == null)
        {
            return Result<QrSessionStatusDto>.Failure("QR Session not found.", "QrSessionNotFound", 404);
        }

        var isExpired = session.IsExpired;
        var status = isExpired && session.Status != QrSessionStatus.Consumed ? QrSessionStatus.Expired : session.Status;

        return Result<QrSessionStatusDto>.Success(new QrSessionStatusDto(
            session.Id,
            status,
            isExpired,
            session.Status == QrSessionStatus.Approved ? session.ExchangeTicket : null,
            session.MobilePublicKey,
            session.EncryptedApprovalPayload,
            session.ClientIpAddress,
            session.ClientUserAgent
        ));
    }
}

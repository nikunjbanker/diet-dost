/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Infrastructure.Persistence.Repositories;

public sealed class EfQrLoginSessionRepository : IQrLoginSessionRepository
{
    private readonly DietTrackerDbContext _db;

    public EfQrLoginSessionRepository(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<QrLoginSession?> TryMarkScannedAsync(string sessionId, DateTime utcNow, CancellationToken ct = default)
    {
        var updated = await _db.QrSessions
            .Where(s => s.Id == sessionId && s.Status == QrSessionStatus.Pending && s.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, QrSessionStatus.Scanned)
                .SetProperty(x => x.ConcurrencyVersion, x => x.ConcurrencyVersion + 1), ct);

        return updated > 0 ? await _db.QrSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct) : null;
    }

    public async Task<QrLoginSession?> TryApproveAsync(
        string sessionId,
        string userId,
        string exchangeTicket,
        string? mobilePublicKey,
        string? encryptedApprovalPayload,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var updated = await _db.QrSessions
            .Where(s => s.Id == sessionId &&
                        (s.Status == QrSessionStatus.Pending || s.Status == QrSessionStatus.Scanned) &&
                        s.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, QrSessionStatus.Approved)
                .SetProperty(x => x.AuthorizedUserId, userId)
                .SetProperty(x => x.ExchangeTicket, exchangeTicket)
                .SetProperty(x => x.MobilePublicKey, mobilePublicKey)
                .SetProperty(x => x.EncryptedApprovalPayload, encryptedApprovalPayload)
                .SetProperty(x => x.ApprovedAtUtc, utcNow)
                .SetProperty(x => x.ConcurrencyVersion, x => x.ConcurrencyVersion + 1), ct);

        return updated > 0 ? await _db.QrSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct) : null;
    }

    public async Task<QrLoginSession?> TryConsumeAsync(string sessionId, string exchangeTicket, DateTime utcNow, CancellationToken ct = default)
    {
        var updated = await _db.QrSessions
            .Where(s => s.Id == sessionId &&
                        s.Status == QrSessionStatus.Approved &&
                        s.ExchangeTicket == exchangeTicket &&
                        s.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, QrSessionStatus.Consumed)
                .SetProperty(x => x.ConsumedAtUtc, utcNow)
                .SetProperty(x => x.ConcurrencyVersion, x => x.ConcurrencyVersion + 1), ct);

        return updated > 0 ? await _db.QrSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct) : null;
    }

    public async Task<QrLoginSession?> TryRejectAsync(string sessionId, DateTime utcNow, CancellationToken ct = default)
    {
        var updated = await _db.QrSessions
            .Where(s => s.Id == sessionId &&
                        (s.Status == QrSessionStatus.Pending || s.Status == QrSessionStatus.Scanned) &&
                        s.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, QrSessionStatus.Rejected)
                .SetProperty(x => x.ConcurrencyVersion, x => x.ConcurrencyVersion + 1), ct);

        return updated > 0 ? await _db.QrSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct) : null;
    }
}

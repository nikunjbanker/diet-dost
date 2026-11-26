/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.Repositories;

public sealed class EfAccountErasurePort : IAccountErasurePort
{
    private readonly DietTrackerDbContext _db;

    public EfAccountErasurePort(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task EraseUserAccountAsync(string userId, DateTime erasedAtUtc, CancellationToken ct = default)
    {
        var pseudoUserId = $"anon_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))[..12].ToLowerInvariant()}";

        await _db.Meals.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAtUtc, erasedAtUtc), ct);
        await _db.Ledgers.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAtUtc, erasedAtUtc), ct);
        await _db.ProgressPhotos.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAtUtc, erasedAtUtc), ct);
        await _db.AiFeedbacks.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAtUtc, erasedAtUtc), ct);
        await _db.Corrections.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAtUtc, erasedAtUtc), ct);

        await _db.VerificationOtps.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.QrSessions.Where(x => x.AuthorizedUserId == userId).ExecuteDeleteAsync(ct);
        await _db.Set<AppNotification>().Where(x => x.RecipientUserId == userId).ExecuteDeleteAsync(ct);
        await _db.AiUsageLogs.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, pseudoUserId), ct);
    }
}

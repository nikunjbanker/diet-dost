/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Repositories;

namespace Nutrition.Infrastructure.Persistence.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly DietTrackerDbContext _db;

    public EfUserRepository(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task UpdateLastActiveBatchAsync(
        IReadOnlyCollection<(string UserId, DateTime LastActiveUtc)> updates,
        CancellationToken ct = default)
    {
        if (updates.Count == 0) return;

        foreach (var (userId, lastActiveUtc) in updates)
        {
            if (string.IsNullOrWhiteSpace(userId)) continue;
            await _db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActiveUtc, lastActiveUtc), ct);
        }
    }
}

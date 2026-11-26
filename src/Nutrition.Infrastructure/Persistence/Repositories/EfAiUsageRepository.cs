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

public sealed class EfAiUsageRepository : IAiUsageRepository
{
    private readonly DietTrackerDbContext _db;

    public EfAiUsageRepository(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AiQuotaWindowSummary> GetQuotaWindowSummaryAsync(
        string userId,
        DateTime todayStartUtc,
        DateTime tomorrowStartUtc,
        DateTime sevenDaysAgoUtc,
        DateTime thirtyDaysAgoUtc,
        int recentLimit = 20,
        CancellationToken ct = default)
    {
        var minWindowUtc = thirtyDaysAgoUtc < todayStartUtc ? thirtyDaysAgoUtc : todayStartUtc;

        var timestamps = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId &&
                        l.TimestampUtc >= minWindowUtc &&
                        l.TimestampUtc < tomorrowStartUtc &&
                        l.IsSuccess &&
                        (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection))
            .Select(l => l.TimestampUtc)
            .Take(5000)
            .ToListAsync(ct);

        var recent = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.TimestampUtc)
            .Take(Math.Clamp(recentLimit, 1, 100))
            .ToListAsync(ct);

        var usedToday = 0;
        var used7D = 0;
        var used30D = 0;

        foreach (var ts in timestamps)
        {
            if (ts >= todayStartUtc && ts < tomorrowStartUtc) usedToday++;
            if (ts >= sevenDaysAgoUtc) used7D++;
            if (ts >= thirtyDaysAgoUtc) used30D++;
        }

        return new AiQuotaWindowSummary(usedToday, used7D, used30D, recent);
    }

    public async Task<Dictionary<string, int>> GetTodaySuccessCountsByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        DateTime todayStartUtc,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<string, int>(StringComparer.Ordinal);

        return await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => userIds.Contains(l.UserId) && l.TimestampUtc >= todayStartUtc && l.IsSuccess)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, StringComparer.Ordinal, ct);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common.Repositories;

public sealed record AiQuotaWindowSummary(
    int UsedToday,
    int UsedLast7Days,
    int UsedLast30Days,
    List<AiUsageLog> RecentOperations
);

/// <summary>
/// Specialized repository port for bounded AI usage quota window queries and admin count aggregations.
/// </summary>
public interface IAiUsageRepository
{
    Task<AiQuotaWindowSummary> GetQuotaWindowSummaryAsync(
        string userId,
        DateTime todayStartUtc,
        DateTime tomorrowStartUtc,
        DateTime sevenDaysAgoUtc,
        DateTime thirtyDaysAgoUtc,
        int recentLimit = 20,
        CancellationToken ct = default);

    Task<Dictionary<string, int>> GetTodaySuccessCountsByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        DateTime todayStartUtc,
        CancellationToken ct = default);
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

public class AiQuotaService : IAiQuotaService
{
    private readonly DietTrackerDbContext _db;
    private readonly ITierConfigurationService _tierConfigService;

    public AiQuotaService(DietTrackerDbContext db, ITierConfigurationService tierConfigService)
    {
        _db = db;
        _tierConfigService = tierConfigService;
    }

    public async Task<AiQuotaCheckResult> CheckQuotaAsync(
        string userId,
        UserTier tier,
        string? userTimezone,
        CancellationToken ct = default)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier, ct);
        var (todayStartUtc, tomorrowStartUtc) = ComputeLocalizedDayBoundariesUtc(userTimezone);

        // Admin & SuperAdmin have infinite quota
        if (config.DailyAiDetectionLimit < 0)
        {
            var adminCount = await _db.AiUsageLogs
                .AsNoTracking()
                .CountAsync(l => l.UserId == userId &&
                                 l.TimestampUtc >= todayStartUtc &&
                                 l.TimestampUtc < tomorrowStartUtc &&
                                 l.IsSuccess &&
                                 (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection), ct);

            return new AiQuotaCheckResult(
                IsAllowed: true,
                RejectionReason: null,
                DailyLimit: -1,
                UsedToday: adminCount,
                RemainingCalls: int.MaxValue,
                ResetsAtUtc: tomorrowStartUtc,
                Tier: tier
            );
        }

        var usedToday = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId &&
                             l.TimestampUtc >= todayStartUtc &&
                             l.TimestampUtc < tomorrowStartUtc &&
                             l.IsSuccess &&
                             (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection), ct);

        if (usedToday >= config.DailyAiDetectionLimit)
        {
            return new AiQuotaCheckResult(
                IsAllowed: false,
                RejectionReason: $"Daily AI detection limit reached for {tier} tier ({usedToday}/{config.DailyAiDetectionLimit}). Resets at midnight.",
                DailyLimit: config.DailyAiDetectionLimit,
                UsedToday: usedToday,
                RemainingCalls: 0,
                ResetsAtUtc: tomorrowStartUtc,
                Tier: tier
            );
        }

        var remaining = config.DailyAiDetectionLimit - usedToday;
        return new AiQuotaCheckResult(
            IsAllowed: true,
            RejectionReason: null,
            DailyLimit: config.DailyAiDetectionLimit,
            UsedToday: usedToday,
            RemainingCalls: remaining,
            ResetsAtUtc: tomorrowStartUtc,
            Tier: tier
        );
    }

    public async Task<AiUsageStatsResult> GetUsageStatsAsync(
        string userId,
        UserTier tier,
        string? userTimezone,
        CancellationToken ct = default)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier, ct);
        var (todayStartUtc, tomorrowStartUtc) = ComputeLocalizedDayBoundariesUtc(userTimezone);

        var sevenDaysAgoUtc = DateTime.UtcNow.AddDays(-7);
        var thirtyDaysAgoUtc = DateTime.UtcNow.AddDays(-30);

        var usedToday = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId &&
                             l.TimestampUtc >= todayStartUtc &&
                             l.TimestampUtc < tomorrowStartUtc &&
                             l.IsSuccess &&
                             (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection), ct);

        var used7D = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId &&
                             l.TimestampUtc >= sevenDaysAgoUtc &&
                             l.IsSuccess &&
                             (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection), ct);

        var used30D = await _db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId &&
                             l.TimestampUtc >= thirtyDaysAgoUtc &&
                             l.IsSuccess &&
                             (l.OperationType == AiOperationType.PhotoDetection || l.OperationType == AiOperationType.TextDetection), ct);

        var recentLogs = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.TimestampUtc)
            .Take(20)
            .ToListAsync(ct);

        var remaining = config.DailyAiDetectionLimit < 0
            ? int.MaxValue
            : Math.Max(0, config.DailyAiDetectionLimit - usedToday);

        return new AiUsageStatsResult(
            Tier: tier,
            DailyLimit: config.DailyAiDetectionLimit,
            UsedToday: usedToday,
            RemainingCalls: remaining,
            ResetsAtUtc: tomorrowStartUtc,
            UsedLast7Days: used7D,
            UsedLast30Days: used30D,
            RecentOperations: recentLogs
        );
    }

    public async Task RecordUsageAsync(
        string userId,
        AiOperationType operationType,
        string modelId,
        int estimatedTokens,
        long latencyMs,
        bool isSuccess,
        string? errorReason = null,
        CancellationToken ct = default)
    {
        var log = new AiUsageLog
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            OperationType = operationType,
            ModelId = modelId,
            EstimatedTokensUsed = estimatedTokens,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            ErrorReason = errorReason,
            TimestampUtc = DateTime.UtcNow
        };

        await _db.AiUsageLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    private static (DateTime TodayStartUtc, DateTime TomorrowStartUtc) ComputeLocalizedDayBoundariesUtc(string? userTimezone)
    {
        var tz = ClinicalDietitianService.GetUserTimeZoneInfo(userTimezone);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayStartLocal = nowLocal.Date;
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, tz);
        var tomorrowStartLocal = todayStartLocal.AddDays(1);
        var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(tomorrowStartLocal, tz);

        return (todayStartUtc, tomorrowStartUtc);
    }
}

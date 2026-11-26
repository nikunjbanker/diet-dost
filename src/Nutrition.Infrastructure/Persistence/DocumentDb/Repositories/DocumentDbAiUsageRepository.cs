/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Driver;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Infrastructure.Persistence.DocumentDb.Repositories;

public sealed class DocumentDbAiUsageRepository : IAiUsageRepository
{
    private readonly IMongoCollection<AiUsageLog> _collection;

    public DocumentDbAiUsageRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        DocumentDbBsonMappings.Register();
        _collection = database.GetCollection<AiUsageLog>("AiUsageLogs");
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

        var successFilter = Builders<AiUsageLog>.Filter.And(
            Builders<AiUsageLog>.Filter.Eq(l => l.UserId, userId),
            Builders<AiUsageLog>.Filter.Gte(l => l.TimestampUtc, minWindowUtc),
            Builders<AiUsageLog>.Filter.Lt(l => l.TimestampUtc, tomorrowStartUtc),
            Builders<AiUsageLog>.Filter.Eq(l => l.IsSuccess, true),
            Builders<AiUsageLog>.Filter.In(l => l.OperationType, new[] { AiOperationType.PhotoDetection, AiOperationType.TextDetection }));

        var timestampsTask = _collection.Find(successFilter)
            .Project(l => l.TimestampUtc)
            .Limit(5000)
            .ToListAsync(ct);

        var recentTask = _collection.Find(Builders<AiUsageLog>.Filter.Eq(l => l.UserId, userId))
            .Sort(Builders<AiUsageLog>.Sort.Descending(l => l.TimestampUtc))
            .Limit(Math.Clamp(recentLimit, 1, 100))
            .ToListAsync(ct);

        await Task.WhenAll(timestampsTask, recentTask);

        var timestamps = await timestampsTask;
        var usedToday = 0;
        var used7D = 0;
        var used30D = 0;

        foreach (var ts in timestamps)
        {
            if (ts >= todayStartUtc && ts < tomorrowStartUtc) usedToday++;
            if (ts >= sevenDaysAgoUtc) used7D++;
            if (ts >= thirtyDaysAgoUtc) used30D++;
        }

        return new AiQuotaWindowSummary(usedToday, used7D, used30D, await recentTask);
    }

    public async Task<Dictionary<string, int>> GetTodaySuccessCountsByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        DateTime todayStartUtc,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<string, int>(StringComparer.Ordinal);

        var filter = Builders<AiUsageLog>.Filter.And(
            Builders<AiUsageLog>.Filter.In(l => l.UserId, userIds),
            Builders<AiUsageLog>.Filter.Gte(l => l.TimestampUtc, todayStartUtc),
            Builders<AiUsageLog>.Filter.Eq(l => l.IsSuccess, true));

        var grouped = await _collection.Aggregate()
            .Match(filter)
            .Group(l => l.UserId, g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped.ToDictionary(x => x.UserId, x => x.Count, StringComparer.Ordinal);
    }
}

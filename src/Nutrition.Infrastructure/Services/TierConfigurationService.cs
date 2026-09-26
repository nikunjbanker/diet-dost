/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

public class TierConfigurationService : ITierConfigurationService
{
    private readonly DietTrackerDbContext _db;
    private static readonly ConcurrentDictionary<UserTier, TierFeatureConfiguration> _cache = new();

    public static void ClearCache() => _cache.Clear();

    public TierConfigurationService(DietTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<TierFeatureConfiguration> GetConfigurationAsync(UserTier tier, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(tier, out var cached))
        {
            return cached;
        }

        var config = await _db.TierConfigurations.AsNoTracking().FirstOrDefaultAsync(t => t.Tier == tier, ct);
        if (config == null)
        {
            config = TierFeatureConfiguration.CreateDefault(tier);
        }

        _cache[tier] = config;
        return config;
    }

    public async Task<List<TierFeatureConfiguration>> GetAllConfigurationsAsync(CancellationToken ct = default)
    {
        var configs = await _db.TierConfigurations.AsNoTracking().ToListAsync(ct);
        if (configs.Count == 0)
        {
            configs = [
                TierFeatureConfiguration.CreateDefault(UserTier.Free),
                TierFeatureConfiguration.CreateDefault(UserTier.Basic),
                TierFeatureConfiguration.CreateDefault(UserTier.Premium),
                TierFeatureConfiguration.CreateDefault(UserTier.SuperAdmin)
            ];
        }

        foreach (var c in configs)
        {
            _cache[c.Tier] = c;
        }

        return configs.OrderBy(c => c.Tier).ToList();
    }

    public async Task<TierFeatureConfiguration> UpdateConfigurationAsync(TierFeatureConfiguration configuration, CancellationToken ct = default)
    {
        var existing = await _db.TierConfigurations.FirstOrDefaultAsync(t => t.Tier == configuration.Tier, ct);
        if (existing != null)
        {
            existing.DailyAiDetectionLimit = configuration.DailyAiDetectionLimit;
            existing.AllowPhotoCompare = configuration.AllowPhotoCompare;
            existing.AllowDataExport = configuration.AllowDataExport;
            existing.AnalyticsHistoryDays = configuration.AnalyticsHistoryDays;
            existing.Description = configuration.Description;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = configuration.UpdatedByUserId;
        }
        else
        {
            configuration.UpdatedAtUtc = DateTime.UtcNow;
            await _db.TierConfigurations.AddAsync(configuration, ct);
        }

        await _db.SaveChangesAsync(ct);
        _cache[configuration.Tier] = configuration;
        return configuration;
    }
}

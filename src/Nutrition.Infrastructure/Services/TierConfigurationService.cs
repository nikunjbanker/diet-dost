/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Domain-driven service managing tier feature configurations.
/// Decoupled from EF Core via IRepository and IUnitOfWork abstractions (Clean Architecture & DIP).
/// </summary>
public class TierConfigurationService : ITierConfigurationService
{
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly ConcurrentDictionary<UserTier, TierFeatureConfiguration> _cache = new();

    public static void ClearCache() => _cache.Clear();

    public TierConfigurationService(
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork unitOfWork)
    {
        _tierRepo = tierRepo ?? throw new ArgumentNullException(nameof(tierRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Backwards-compatible internal constructor for test fixtures.
    /// Internal so DI container never sees ambiguous public constructors.
    /// </summary>
    internal TierConfigurationService(DietTrackerDbContext db)
        : this(new EfRepository<TierFeatureConfiguration>(db, NullLogger<EfRepository<TierFeatureConfiguration>>.Instance),
               new EfUnitOfWork(db, NullLogger<EfUnitOfWork>.Instance))
    {
    }

    public async Task<TierFeatureConfiguration> GetConfigurationAsync(UserTier tier, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(tier, out var cached))
        {
            return cached;
        }

        var config = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == tier, ct);
        if (config == null)
        {
            config = TierFeatureConfiguration.CreateDefault(tier);
        }

        _cache[tier] = config;
        return config;
    }

    public async Task<List<TierFeatureConfiguration>> GetAllConfigurationsAsync(CancellationToken ct = default)
    {
        var configs = await _tierRepo.GetAllAsync(ct);
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
        var existing = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == configuration.Tier, ct);
        if (existing != null)
        {
            existing.DailyAiDetectionLimit = configuration.DailyAiDetectionLimit;
            existing.AllowPhotoCompare = configuration.AllowPhotoCompare;
            existing.AllowDataExport = configuration.AllowDataExport;
            existing.AnalyticsHistoryDays = configuration.AnalyticsHistoryDays;
            existing.Description = configuration.Description;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = configuration.UpdatedByUserId;
            await _tierRepo.UpdateAsync(existing, ct);
        }
        else
        {
            configuration.UpdatedAtUtc = DateTime.UtcNow;
            await _tierRepo.AddAsync(configuration, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _cache[configuration.Tier] = configuration;
        return configuration;
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.WebGateway.Services;

/// <summary>
/// Thread-safe in-memory presence tracker and database persistence debouncer.
/// Caches recent activity and flushes to the database at most once every 2 minutes per active user,
/// completely eliminating write amplification while keeping the Admin UI and Azure Monitor metrics real-time.
/// </summary>
public class UserPresenceTracker : IUserPresenceTracker
{
    private class PresenceEntry
    {
        public required string UserId { get; set; }
        public UserTier Tier { get; set; }
        public UserRole Role { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public DateTime LastFlushedUtc { get; set; }
        public bool HasPendingDbUpdate { get; set; }
    }

    private readonly ConcurrentDictionary<string, PresenceEntry> _presenceCache = new();
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IRepository<ApplicationUser>? _testUserRepo;
    private readonly IUnitOfWork? _testUow;
    private readonly ILogger<UserPresenceTracker> _logger;

    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CacheRetentionWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// Runtime DI constructor consuming IServiceScopeFactory to safely invoke scoped EF Core repositories.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public UserPresenceTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<UserPresenceTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Test constructor allowing direct injection of mocked or in-memory repositories.
    /// </summary>
    public UserPresenceTracker(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<UserPresenceTracker> logger)
    {
        _testUserRepo = userRepo;
        _testUow = uow;
        _logger = logger;
    }

    public void TrackActivity(string userId, UserTier tier, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var now = DateTime.UtcNow;

        _presenceCache.AddOrUpdate(
            userId,
            _ => new PresenceEntry
            {
                UserId = userId,
                Tier = tier,
                Role = role,
                LastSeenUtc = now,
                LastFlushedUtc = DateTime.MinValue,
                HasPendingDbUpdate = true
            },
            (_, existing) =>
            {
                existing.Tier = tier;
                existing.Role = role;
                existing.LastSeenUtc = now;

                if (now - existing.LastFlushedUtc >= DebounceWindow)
                {
                    existing.HasPendingDbUpdate = true;
                }

                return existing;
            });
    }

    public async Task FlushDebouncedUpdatesAsync(CancellationToken ct = default)
    {
        var pendingUpdates = _presenceCache.Values
            .Where(e => e.HasPendingDbUpdate)
            .ToList();

        if (pendingUpdates.Count == 0)
        {
            PruneStaleEntries();
            return;
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IRepository<ApplicationUser>>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await FlushWithRepositoriesAsync(userRepo, uow, pendingUpdates, ct);
        }
        else if (_testUserRepo != null && _testUow != null)
        {
            await FlushWithRepositoriesAsync(_testUserRepo, _testUow, pendingUpdates, ct);
        }

        PruneStaleEntries();
    }

    private async Task FlushWithRepositoriesAsync(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        List<PresenceEntry> pendingUpdates,
        CancellationToken ct)
    {
        int updatedCount = 0;
        foreach (var entry in pendingUpdates)
        {
            try
            {
                var user = await userRepo.GetByIdAsync(entry.UserId, ct);
                if (user != null && !user.IsDeleted)
                {
                    user.LastActiveUtc = entry.LastSeenUtc;
                    await userRepo.UpdateAsync(user, ct);
                    entry.LastFlushedUtc = entry.LastSeenUtc;
                    entry.HasPendingDbUpdate = false;
                    updatedCount++;
                }
                else
                {
                    entry.HasPendingDbUpdate = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush debounced presence update for User {UserId}", entry.UserId);
            }
        }

        if (updatedCount > 0)
        {
            await uow.SaveChangesAsync(ct);
            _logger.LogDebug("Flushed debounced presence updates for {Count} active users", updatedCount);
        }
    }

    public int GetActiveUserCount(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return _presenceCache.Values.Count(e => e.LastSeenUtc >= cutoff);
    }

    public IReadOnlyDictionary<UserTier, int> GetActiveCountByTier(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        var counts = new Dictionary<UserTier, int>
        {
            { UserTier.Free, 0 },
            { UserTier.Basic, 0 },
            { UserTier.Premium, 0 },
            { UserTier.SuperAdmin, 0 }
        };

        var activeEntries = _presenceCache.Values
            .Where(e => e.LastSeenUtc >= cutoff);

        foreach (var entry in activeEntries)
        {
            if (counts.ContainsKey(entry.Tier))
            {
                counts[entry.Tier]++;
            }
            else
            {
                counts[entry.Tier] = 1;
            }
        }

        return counts;
    }

    private void PruneStaleEntries()
    {
        var evictionCutoff = DateTime.UtcNow - CacheRetentionWindow;
        foreach (var (key, value) in _presenceCache)
        {
            if (value.LastSeenUtc < evictionCutoff && !value.HasPendingDbUpdate)
            {
                _presenceCache.TryRemove(key, out _);
            }
        }
    }
}


/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Thread-safe in-memory presence tracker and database persistence debouncer.
/// Records authenticated user activity and calculates sliding-window active metrics without leaking PII.
/// </summary>
public interface IUserPresenceTracker
{
    /// <summary>
    /// Records activity for an authenticated user.
    /// Debounces database persistence to prevent write amplification.
    /// </summary>
    void TrackActivity(string userId, UserTier tier, UserRole role);

    /// <summary>
    /// Flushes debounced in-memory active timestamps to the database repository.
    /// </summary>
    Task FlushDebouncedUpdatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the count of active users within the given sliding window.
    /// </summary>
    int GetActiveUserCount(TimeSpan window);

    /// <summary>
    /// Gets the count of active users within the given sliding window grouped by subscription tier.
    /// </summary>
    IReadOnlyDictionary<UserTier, int> GetActiveCountByTier(TimeSpan window);
}

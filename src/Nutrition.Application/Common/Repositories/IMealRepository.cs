/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Common.Repositories;

/// <summary>
/// Lightweight projection of meal log timestamps for streak calculation without hydrating FoodItems.
/// </summary>
public sealed record MealStreakEntry(DateTime LoggedAt, double TotalCalories);

/// <summary>
/// Specialized repository port for bounded MealLog aggregate queries and offline idempotency lookups.
/// </summary>
public interface IMealRepository
{
    Task<List<MealLog>> GetByUserAndUtcRangeAsync(
        string userId,
        DateTime fromUtc,
        DateTime toUtc,
        MealType? mealType = null,
        CancellationToken ct = default);

    Task<List<MealStreakEntry>> GetLoggedEntriesForStreakAsync(
        string userId,
        DateTime sinceUtc,
        CancellationToken ct = default);

    Task<MealLog?> GetByClientMutationIdAsync(
        string userId,
        string clientMutationId,
        CancellationToken ct = default);
}

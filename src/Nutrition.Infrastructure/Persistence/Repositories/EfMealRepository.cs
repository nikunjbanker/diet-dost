/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Infrastructure.Persistence.Repositories;

public sealed class EfMealRepository : IMealRepository
{
    private readonly DietTrackerDbContext _db;

    public EfMealRepository(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<MealLog>> GetByUserAndUtcRangeAsync(
        string userId,
        DateTime fromUtc,
        DateTime toUtc,
        MealType? mealType = null,
        CancellationToken ct = default)
    {
        var query = _db.Meals
            .AsNoTracking()
            .Where(m => m.UserId == userId && !m.IsDeleted && m.LoggedAt >= fromUtc && m.LoggedAt < toUtc);

        if (mealType.HasValue)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        return await query
            .OrderByDescending(m => m.LoggedAt)
            .Take(1000)
            .ToListAsync(ct);
    }

    public async Task<List<MealStreakEntry>> GetLoggedEntriesForStreakAsync(
        string userId,
        DateTime sinceUtc,
        CancellationToken ct = default)
    {
        return await _db.Meals
            .AsNoTracking()
            .Where(m => m.UserId == userId && !m.IsDeleted && m.LoggedAt >= sinceUtc)
            .OrderByDescending(m => m.LoggedAt)
            .Take(2000)
            .Select(m => new MealStreakEntry(m.LoggedAt, m.TotalCalories))
            .ToListAsync(ct);
    }

    public async Task<MealLog?> GetByClientMutationIdAsync(
        string userId,
        string clientMutationId,
        CancellationToken ct = default)
    {
        return await _db.Meals
            .AsNoTracking()
            .Where(m => m.UserId == userId && !m.IsDeleted && m.ClientMutationId == clientMutationId)
            .FirstOrDefaultAsync(ct);
    }
}

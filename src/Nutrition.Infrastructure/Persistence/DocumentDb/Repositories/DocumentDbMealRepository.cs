/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Driver;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Infrastructure.Persistence.DocumentDb.Repositories;

public sealed class DocumentDbMealRepository : IMealRepository
{
    private readonly IMongoCollection<MealLog> _collection;

    public DocumentDbMealRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        DocumentDbBsonMappings.Register();
        _collection = database.GetCollection<MealLog>("Meals");
    }

    public async Task<List<MealLog>> GetByUserAndUtcRangeAsync(
        string userId,
        DateTime fromUtc,
        DateTime toUtc,
        MealType? mealType = null,
        CancellationToken ct = default)
    {
        var filter = Builders<MealLog>.Filter.And(
            Builders<MealLog>.Filter.Eq(m => m.UserId, userId),
            Builders<MealLog>.Filter.Ne(m => m.IsDeleted, true),
            Builders<MealLog>.Filter.Gte(m => m.LoggedAt, fromUtc),
            Builders<MealLog>.Filter.Lt(m => m.LoggedAt, toUtc));

        if (mealType.HasValue)
        {
            filter = Builders<MealLog>.Filter.And(filter, Builders<MealLog>.Filter.Eq(m => m.MealType, mealType.Value));
        }

        return await _collection.Find(filter)
            .Sort(Builders<MealLog>.Sort.Descending(m => m.LoggedAt))
            .Limit(1000)
            .ToListAsync(ct);
    }

    public async Task<List<MealStreakEntry>> GetLoggedEntriesForStreakAsync(
        string userId,
        DateTime sinceUtc,
        CancellationToken ct = default)
    {
        var filter = Builders<MealLog>.Filter.And(
            Builders<MealLog>.Filter.Eq(m => m.UserId, userId),
            Builders<MealLog>.Filter.Ne(m => m.IsDeleted, true),
            Builders<MealLog>.Filter.Gte(m => m.LoggedAt, sinceUtc));

        return await _collection.Find(filter)
            .Sort(Builders<MealLog>.Sort.Descending(m => m.LoggedAt))
            .Limit(2000)
            .Project(m => new MealStreakEntry(m.LoggedAt, m.TotalCalories))
            .ToListAsync(ct);
    }

    public async Task<MealLog?> GetByClientMutationIdAsync(
        string userId,
        string clientMutationId,
        CancellationToken ct = default)
    {
        var filter = Builders<MealLog>.Filter.And(
            Builders<MealLog>.Filter.Eq(m => m.UserId, userId),
            Builders<MealLog>.Filter.Ne(m => m.IsDeleted, true),
            Builders<MealLog>.Filter.Eq(m => m.ClientMutationId, clientMutationId));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }
}

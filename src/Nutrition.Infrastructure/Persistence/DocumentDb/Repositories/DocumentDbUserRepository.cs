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

public sealed class DocumentDbUserRepository : IUserRepository
{
    private readonly IMongoCollection<ApplicationUser> _collection;

    public DocumentDbUserRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        DocumentDbBsonMappings.Register();
        _collection = database.GetCollection<ApplicationUser>("Users");
    }

    public async Task UpdateLastActiveBatchAsync(
        IReadOnlyCollection<(string UserId, DateTime LastActiveUtc)> updates,
        CancellationToken ct = default)
    {
        if (updates.Count == 0) return;

        var models = new List<WriteModel<ApplicationUser>>(updates.Count);
        foreach (var (userId, lastActiveUtc) in updates)
        {
            if (string.IsNullOrWhiteSpace(userId)) continue;
            var filter = Builders<ApplicationUser>.Filter.Eq("_id", userId);
            var update = Builders<ApplicationUser>.Update.Set(u => u.LastActiveUtc, lastActiveUtc);
            models.Add(new UpdateOneModel<ApplicationUser>(filter, update));
        }

        if (models.Count > 0)
        {
            await _collection.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false }, ct);
        }
    }
}

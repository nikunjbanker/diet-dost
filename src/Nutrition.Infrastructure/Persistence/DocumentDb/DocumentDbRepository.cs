/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nutrition.Application.Common;
using Nutrition.Domain.Common;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.DocumentDb;

/// <summary>
/// Domain-driven repository implementation targeting Azure DocumentDB (Free Tier - 32 GB Dedicated Storage)
/// via the official MongoDB.Driver. Implements automatic soft-delete filtering, universal UTC DateTime
/// persistence, and string GUID _id primary key mapping.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class DocumentDbRepository<T> : IRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;
    private readonly ILogger<DocumentDbRepository<T>> _logger;
    private static readonly PropertyInfo? IdProperty = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("Key");

    public DocumentDbRepository(IMongoDatabase database, ILogger<DocumentDbRepository<T>> logger)
    {
        _logger = logger;
        var collectionName = ResolveCollectionName();
        _collection = database.GetCollection<T>(collectionName);
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
            return await _collection.Find(filter).FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType} [RecordId: {RecordId}]. Error: {Message}",
                nameof(GetByIdAsync), typeof(T).Name, id, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<T> filter = Builders<T>.Filter.Empty;
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.Ne("IsDeleted", true);
            }
            return await _collection.Find(filter).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(GetAllAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<T> filter = predicate;
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
            return await _collection.Find(filter).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(FindAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<T> filter = predicate;
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
            return await _collection.Find(filter).FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(FirstOrDefaultAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<T> filter = predicate;
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
            return await _collection.Find(filter).AnyAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(AnyAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            FilterDefinition<T> filter = predicate;
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
            var count = await _collection.CountDocumentsAsync(filter, null, ct);
            return (int)count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(CountAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> FindDeletedAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        try
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                return new List<T>();
            }

            FilterDefinition<T> filter = Builders<T>.Filter.Eq("IsDeleted", true);
            if (predicate != null)
            {
                filter = Builders<T>.Filter.And(filter, predicate);
            }
            return await _collection.Find(filter).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(FindDeletedAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public IQueryable<T> Query()
    {
        var query = _collection.AsQueryable();
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
        {
            var param = Expression.Parameter(typeof(T), "e");
            var prop = Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Not(prop);
            var lambda = Expression.Lambda<Func<T, bool>>(notDeleted, param);
            query = query.Where(lambda);
        }
        return query;
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(entity, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(AddAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            var id = GetEntityId(entity);
            var filter = Builders<T>.Filter.Eq("_id", id);
            await _collection.ReplaceOneAsync(filter, entity, new ReplaceOptions { IsUpsert = true }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(UpdateAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                var entity = await GetByIdAsync(id, ct);
                if (entity is ISoftDeletable soft)
                {
                    soft.IsDeleted = true;
                    soft.DeletedAtUtc = DateTime.UtcNow;
                    await UpdateAsync(entity, ct);
                    return;
                }
            }

            var filter = Builders<T>.Filter.Eq("_id", id);
            await _collection.DeleteOneAsync(filter, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType} [RecordId: {RecordId}]. Error: {Message}",
                nameof(DeleteAsync), typeof(T).Name, id, ex.Message);
            throw;
        }
    }

    private static string GetEntityId(T entity)
    {
        if (IdProperty != null)
        {
            var val = IdProperty.GetValue(entity);
            if (val != null) return val.ToString()!;
        }
        throw new InvalidOperationException($"Entity type '{typeof(T).Name}' does not declare an Id or Key property.");
    }

    private static string ResolveCollectionName()
    {
        var type = typeof(T);
        return type.Name switch
        {
            nameof(UserProfile) => "Profiles",
            nameof(MealLog) => "Meals",
            nameof(FoodItemRecord) => "FoodItems",
            nameof(DailyCalorieLedger) => "Ledgers",
            nameof(UserCorrectionRecord) => "Corrections",
            nameof(ProgressPhoto) => "ProgressPhotos",
            nameof(AiDetectionFeedbackRecord) => "AiFeedbacks",
            nameof(ApplicationUser) => "Users",
            nameof(VerificationOtp) => "VerificationOtps",
            nameof(TierFeatureConfiguration) => "TierConfigurations",
            nameof(AiUsageLog) => "AiUsageLogs",
            nameof(AppSecret) => "AppSecrets",
            nameof(QrLoginSession) => "QrSessions",
            nameof(AppNotification) => "AppNotifications",
            _ => type.Name + "s"
        };
    }
}

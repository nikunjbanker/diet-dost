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

    public async Task<List<T>> FindAsync(QuerySpec<T> spec, CancellationToken ct = default)
    {
        try
        {
            return await BuildFindFluent(spec, deletedOnly: false).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(FindAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<List<TResult>> ProjectAsync<TResult>(QuerySpec<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default)
    {
        try
        {
            return await BuildFindFluent(spec, deletedOnly: false).Project(selector).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(ProjectAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> FindDeletedAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        return await FindDeletedAsync(new QuerySpec<T>(Filter: predicate, IncludeDeleted: true), ct);
    }

    public async Task<List<T>> FindDeletedAsync(QuerySpec<T> spec, CancellationToken ct = default)
    {
        try
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                return new List<T>();
            }

            return await BuildFindFluent(spec, deletedOnly: true).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocumentDB error in {Operation} for entity {EntityType}. Error: {Message}",
                nameof(FindDeletedAsync), typeof(T).Name, ex.Message);
            throw;
        }
    }

    private IFindFluent<T, T> BuildFindFluent(QuerySpec<T> spec, bool deletedOnly)
    {
        FilterDefinition<T> filter = spec.Filter ?? Builders<T>.Filter.Empty;
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
        {
            if (deletedOnly)
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Eq("IsDeleted", true));
            }
            else if (!spec.IncludeDeleted)
            {
                filter = Builders<T>.Filter.And(filter, Builders<T>.Filter.Ne("IsDeleted", true));
            }
        }

        var find = _collection.Find(filter);
        if (spec.OrderBy != null)
        {
            var fieldExpr = UnwrapSortExpression(spec.OrderBy);
            find = spec.Descending
                ? find.Sort(Builders<T>.Sort.Descending(fieldExpr))
                : find.Sort(Builders<T>.Sort.Ascending(fieldExpr));
        }

        if (spec.Skip.HasValue && spec.Skip.Value > 0)
        {
            find = find.Skip(spec.Skip.Value);
        }

        if (spec.Take.HasValue && spec.Take.Value > 0)
        {
            find = find.Limit(Math.Clamp(spec.Take.Value, 1, 500));
        }

        return find;
    }

    private static Expression<Func<T, object>> UnwrapSortExpression(Expression<Func<T, object>> expr)
    {
        if (expr.Body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            return Expression.Lambda<Func<T, object>>(expr.Body, expr.Parameters);
        }
        return expr;
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

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Persistence;

public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly DietTrackerDbContext _context;
    private readonly DbSet<T> _dbSet;
    private readonly ILogger<EfRepository<T>> _logger;

    public EfRepository(DietTrackerDbContext context, ILogger<EfRepository<T>> logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.FindAsync(new object[] { id }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType} [RecordId: {RecordId}]. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(GetByIdAsync), typeof(T).Name, id, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(GetAllAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.Where(predicate).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(FindAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.FirstOrDefaultAsync(predicate, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(FirstOrDefaultAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.AnyAsync(predicate, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(AnyAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        try
        {
            return await _dbSet.CountAsync(predicate, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(CountAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            await _dbSet.AddAsync(entity, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(AddAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Update(entity);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType}. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(UpdateAsync), typeof(T).Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var entity = await GetByIdAsync(id, ct);
            if (entity != null)
            {
                _dbSet.Remove(entity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Database error in {Operation} for entity {EntityType} [RecordId: {RecordId}]. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                nameof(DeleteAsync), typeof(T).Name, id, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}

public class EfUnitOfWork : IUnitOfWork
{
    private readonly DietTrackerDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;

    public EfUnitOfWork(DietTrackerDbContext context, ILogger<EfUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException dbEx)
        {
            // Non-PII diagnostic details: Entity Types and Entry States only
            var entityDetails = dbEx.Entries
                .Select(e => $"Entity: {e.Entity.GetType().Name}, State: {e.State}")
                .ToList();

            var entriesSummary = string.Join("; ", entityDetails);

            _logger.LogError(dbEx,
                "Database SaveChanges failed. AffectedEntries: [{EntriesSummary}]. DbUpdateException: {Message}. InnerException: {InnerMessage}",
                entriesSummary, dbEx.Message, dbEx.InnerException?.Message ?? "None");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected database error in SaveChangesAsync. ErrorType: {ErrorType}, Message: {ErrorMessage}",
                ex.GetType().Name, ex.Message);
            throw;
        }
    }
}


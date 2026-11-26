/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;

namespace Nutrition.Application.Common;

/// <summary>
/// Provider-agnostic query specification supporting filtering, sorting, pagination, and soft-delete visibility.
/// Replaces leaky IQueryable abstractions across SQLite, Azure SQL, and Azure DocumentDB.
/// </summary>
public sealed record QuerySpec<T>(
    Expression<Func<T, bool>>? Filter = null,
    Expression<Func<T, object>>? OrderBy = null,
    bool Descending = false,
    int? Skip = null,
    int? Take = null,
    bool IncludeDeleted = false
);

/// <summary>
/// Read-side repository contract (Interface Segregation Principle).
/// </summary>
public interface IReadRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> FindAsync(QuerySpec<T> spec, CancellationToken ct = default);
    Task<List<TResult>> ProjectAsync<TResult>(QuerySpec<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> FindDeletedAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<List<T>> FindDeletedAsync(QuerySpec<T> spec, CancellationToken ct = default);
}

/// <summary>
/// Write-side repository contract (Interface Segregation Principle).
/// </summary>
public interface IWriteRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Domain-driven repository abstraction for entity persistence combining read and write capabilities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T> where T : class
{
}

/// <summary>
/// Unit of work abstraction coordinating persistence commit boundaries across repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

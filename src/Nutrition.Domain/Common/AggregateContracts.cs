/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Common;

/// <summary>
/// Marker interface for Domain-Driven Design Aggregate Roots with a string identifier.
/// </summary>
public interface IAggregateRoot
{
    string Id { get; }
}

/// <summary>
/// Contract for aggregate roots supporting optimistic concurrency control (CAS / RowVersion)
/// across SQLite, Azure SQL, and Azure DocumentDB.
/// </summary>
public interface IConcurrencyAware
{
    long ConcurrencyVersion { get; set; }
}

/// <summary>
/// Domain exception thrown when an optimistic concurrency check fails during persistence.
/// </summary>
public sealed class DbConcurrencyConflictException : Exception
{
    public string EntityType { get; }
    public string RecordId { get; }

    public DbConcurrencyConflictException(string entityType, string recordId)
        : base($"Optimistic concurrency conflict detected for {entityType} [Id: {recordId}].")
    {
        EntityType = entityType;
        RecordId = recordId;
    }

    public DbConcurrencyConflictException(string entityType, string recordId, Exception innerException)
        : base($"Optimistic concurrency conflict detected for {entityType} [Id: {recordId}].", innerException)
    {
        EntityType = entityType;
        RecordId = recordId;
    }
}

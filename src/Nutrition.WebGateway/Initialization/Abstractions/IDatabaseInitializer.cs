/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

namespace Nutrition.WebGateway.Initialization.Abstractions;

/// <summary>
/// Strategy interface for provider-specific database schema verification, PRAGMA configuration,
/// DDL execution, and index management.
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Gets the provider name handled by this strategy (e.g. Sqlite, AzureSql, DocumentDb).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Verifies and initializes the database schema, tables, collections, and indexes.
    /// </summary>
    Task InitializeSchemaAsync(CancellationToken cancellationToken = default);
}

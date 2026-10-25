/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Options;

/// <summary>
/// Strongly-typed Options for Database persistence provider and connection strings.
/// Bound to configuration section "Database" and connection strings.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "AzureSql";
    public string ConnectionString { get; set; } = "Data Source=diettracker.db";

    /// <summary>
    /// When true, SQLite active connections use high-performance local ephemeral storage (/tmp),
    /// backed by asynchronous/graceful synchronization to a persistent network storage vault (/app/data).
    /// </summary>
    public bool UseEphemeralActiveCache { get; set; }

    /// <summary>
    /// Path to the durable network-mounted storage vault file (e.g., /app/data/diet_dost.db).
    /// </summary>
    public string? StorageVaultPath { get; set; }

    /// <summary>
    /// Path to the active local ephemeral database file (e.g., /tmp/diet_dost.db).
    /// </summary>
    public string? ActiveDbPath { get; set; }
}

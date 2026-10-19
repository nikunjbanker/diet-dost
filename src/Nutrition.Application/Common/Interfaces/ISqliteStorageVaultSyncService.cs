/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Service contract for synchronizing the active local ephemeral SQLite database (/tmp)
/// with the durable Azure Files network storage vault (/app/data).
/// </summary>
public interface ISqliteStorageVaultSyncService
{
    /// <summary>
    /// Gets whether ephemeral active caching and vault synchronization are active.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Path to the active ephemeral SQLite database on local high-performance storage.
    /// </summary>
    string? ActiveDbPath { get; }

    /// <summary>
    /// Path to the durable cloud storage vault SQLite database on the network mount.
    /// </summary>
    string? StorageVaultPath { get; }

    /// <summary>
    /// Synchronizes the active database state to the durable storage vault.
    /// </summary>
    Task<bool> SyncActiveToVaultAsync(CancellationToken cancellationToken = default);
}

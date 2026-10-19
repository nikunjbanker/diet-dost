/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Persistence.Services;

/// <summary>
/// Background service and synchronization coordinator that mirrors the local high-performance
/// ephemeral SQLite database (/tmp) to the durable Azure Files SMB storage vault (/app/data).
/// </summary>
public sealed class SqliteStorageVaultSyncService : BackgroundService, ISqliteStorageVaultSyncService, IDisposable
{
    private readonly ILogger<SqliteStorageVaultSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly TimeSpan _syncInterval = TimeSpan.FromSeconds(30);

    public bool IsEnabled { get; }
    public string? ActiveDbPath { get; }
    public string? StorageVaultPath { get; }

    public SqliteStorageVaultSyncService(
        ILogger<SqliteStorageVaultSyncService> logger,
        string? activeDbPath,
        string? storageVaultPath,
        bool enabled)
    {
        _logger = logger;
        ActiveDbPath = activeDbPath;
        StorageVaultPath = storageVaultPath;
        IsEnabled = enabled &&
                    !string.IsNullOrWhiteSpace(activeDbPath) &&
                    !string.IsNullOrWhiteSpace(storageVaultPath);
    }

    public async Task<bool> SyncActiveToVaultAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(ActiveDbPath) || string.IsNullOrWhiteSpace(StorageVaultPath))
        {
            return false;
        }

        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("[VAULT-SYNC] Skipping synchronization; previous sync operation is still in progress.");
            }
            return false;
        }

        try
        {
            if (!File.Exists(ActiveDbPath))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[VAULT-SYNC] Active SQLite database does not exist at '{ActivePath}'; skipping sync.", ActiveDbPath);
                }
                return false;
            }

            // Checkpoint active WAL entries into the primary database file prior to file copy
            try
            {
                var connStr = new SqliteConnectionStringBuilder
                {
                    DataSource = ActiveDbPath,
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString();

                await using var conn = new SqliteConnection(connStr);
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "[VAULT-SYNC] Non-fatal WAL checkpoint warning on '{ActivePath}'. Proceeding with file copy.", ActiveDbPath);
                }
            }

            var targetDir = Path.GetDirectoryName(StorageVaultPath);
            if (!string.IsNullOrWhiteSpace(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Copy(ActiveDbPath, StorageVaultPath, overwrite: true);

            var fileLen = new FileInfo(StorageVaultPath).Length;
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[VAULT-SYNC] Successfully synchronized active SQLite database ({Bytes} bytes) to durable vault '{VaultPath}'.", fileLen, StorageVaultPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "[VAULT-SYNC] Critical error synchronizing active SQLite database '{ActivePath}' to durable vault '{VaultPath}'.", ActiveDbPath, StorageVaultPath);
            }
            return false;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[VAULT-SYNC] Ephemeral active cache and vault sync is disabled for this environment.");
            }
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[VAULT-SYNC] Ephemeral SQLite vault synchronization background service started. Interval: {Interval}s. Active: '{ActivePath}' -> Vault: '{VaultPath}'.",
                _syncInterval.TotalSeconds, ActiveDbPath, StorageVaultPath);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
                await SyncActiveToVaultAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "[VAULT-SYNC] Unexpected failure during periodic vault sync loop.");
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (IsEnabled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[VAULT-SYNC] Host termination detected. Executing final graceful flush to durable vault '{VaultPath}'...", StorageVaultPath);
            }
            try
            {
                await SyncActiveToVaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "[VAULT-SYNC] Failed final graceful flush during host shutdown.");
                }
            }
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _syncLock.Dispose();
        base.Dispose();
    }
}

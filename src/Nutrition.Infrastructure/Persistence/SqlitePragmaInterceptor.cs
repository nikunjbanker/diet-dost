/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nutrition.Infrastructure.Persistence;

/// <summary>
/// EF Core connection interceptor that ensures every opened SQLite connection
/// has the appropriate journal mode (WAL for local/SSD, DELETE for Azure Files SMB network mounts)
/// and a 30000ms busy timeout configured to prevent concurrency lock errors (SQLITE_BUSY / SQLite Error 5).
/// </summary>
public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private static string ResolvePragmaScript(string? connectionString)
    {
        var configuredMode = Environment.GetEnvironmentVariable("Database__JournalMode")
            ?? Environment.GetEnvironmentVariable("Database:JournalMode");

        if (!string.IsNullOrWhiteSpace(configuredMode))
        {
            return $"PRAGMA journal_mode={configuredMode}; PRAGMA busy_timeout=30000;";
        }

        // Azure Files SMB / CIFS network mounts do not support POSIX shared memory mmap (.shm),
        // requiring rollback journal mode (DELETE) per SDD-05 §2.1.
        var isNetworkMount = !string.IsNullOrWhiteSpace(connectionString) &&
            connectionString.Contains("/app/data", StringComparison.OrdinalIgnoreCase);

        var mode = isNetworkMount ? "DELETE" : "WAL";
        return $"PRAGMA journal_mode={mode}; PRAGMA busy_timeout=30000;";
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = ResolvePragmaScript(connection.ConnectionString);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignore PRAGMA failures on non-SQLite or closed connections
        }

        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = ResolvePragmaScript(connection.ConnectionString);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignore PRAGMA failures on non-SQLite or closed connections
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}


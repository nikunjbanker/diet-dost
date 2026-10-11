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
/// has WAL journal mode enabled and a 5000ms busy timeout configured to prevent
/// concurrency lock errors (SQLITE_BUSY / SQLite Error 5).
/// </summary>
public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
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
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignore PRAGMA failures on non-SQLite or closed connections
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}

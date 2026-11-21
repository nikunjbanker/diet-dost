/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data;
using Microsoft.Data.SqlClient;

#pragma warning disable CA2100 // Dynamic SQL commands use verified table and column metadata discovered from INFORMATION_SCHEMA
namespace Nutrition.Tools.DataMigrator.Services;

/// <summary>
/// Source reader extracting domain tables and records from Azure SQL Database.
/// Enables migrating data out of Azure SQL into Azure DocumentDB or SQLite.
/// </summary>
public class AzureSqlSourceReader : IDisposable
{
    private readonly string _connectionString;
    private SqlConnection? _connection;

    public AzureSqlSourceReader(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task OpenAsync()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            ConnectTimeout = 120,
            ConnectRetryCount = 4,
            ConnectRetryInterval = 10,
            Encrypt = true,
            TrustServerCertificate = false
        };

        _connection = new SqlConnection(builder.ConnectionString);
        await _connection.OpenAsync();
    }

    public async Task<List<SqliteTableMetadata>> DiscoverTablesAsync()
    {
        EnsureConnected();
        var tables = new List<SqliteTableMetadata>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' 
              AND TABLE_NAME NOT IN ('__EFMigrationsHistory', 'sysdiagrams')
            ORDER BY TABLE_NAME;";

        var tableNames = new List<string>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        foreach (var tableName in tableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            var primaryKeys = columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();

            using var countCmd = _connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT_BIG(1) FROM [{tableName}];";
            var countObj = await countCmd.ExecuteScalarAsync();
            var rowCount = Convert.ToInt32(countObj ?? 0);

            tables.Add(new SqliteTableMetadata(tableName, rowCount, columns, primaryKeys));
        }

        return tables;
    }

    public async Task<List<SqliteColumnMetadata>> GetTableColumnsAsync(string tableName)
    {
        EnsureConnected();
        var columns = new List<SqliteColumnMetadata>();

        // 1. Discover primary keys
        var primaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pkCmd = _connection!.CreateCommand())
        {
            pkCmd.CommandText = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1
                  AND TABLE_NAME = @tableName;";
            pkCmd.Parameters.AddWithValue("@tableName", tableName);
            using var pkReader = await pkCmd.ExecuteReaderAsync();
            while (await pkReader.ReadAsync())
            {
                primaryKeys.Add(pkReader.GetString(0));
            }
        }

        // 2. Discover column specifications
        using (var colCmd = _connection.CreateCommand())
        {
            colCmd.CommandText = @"
                SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION;";
            colCmd.Parameters.AddWithValue("@tableName", tableName);

            using var reader = await colCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var ordinal = reader.GetInt32(0);
                var name = reader.GetString(1);
                var dataType = reader.GetString(2);
                var isNullable = reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase);
                var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
                var isPk = primaryKeys.Contains(name);

                columns.Add(new SqliteColumnMetadata(ordinal, name, dataType, !isNullable, defaultValue, isPk));
            }
        }

        return columns;
    }

    public async IAsyncEnumerable<Dictionary<string, object?>> StreamRowsAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"SELECT * FROM [{tableName}];";
        cmd.CommandTimeout = 120;
        using var reader = await cmd.ExecuteReaderAsync();

        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnNames[i]] = val;
            }
            yield return row;
        }
    }

    private void EnsureConnected()
    {
        if (_connection == null || _connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Azure SQL connection is not open. Call OpenAsync() first.");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        GC.SuppressFinalize(this);
    }
}

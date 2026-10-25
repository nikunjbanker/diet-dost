/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;

namespace Nutrition.Tools.DataMigrator.Services;

public record SqliteColumnMetadata(
    int ColumnId,
    string Name,
    string DeclaredType,
    bool IsNotNull,
    string? DefaultValue,
    bool IsPrimaryKey
);

public record SqliteTableMetadata(
    string TableName,
    int RowCount,
    List<SqliteColumnMetadata> Columns,
    List<string> PrimaryKeyColumns
);

public class SqliteSourceReader : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public SqliteSourceReader(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        SQLitePCL.Batteries.Init();
    }

    public async Task OpenAsync()
    {
        if (!File.Exists(_dbPath))
        {
            throw new FileNotFoundException($"SQLite source database file not found at: {_dbPath}");
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync();
    }

    public async Task<List<SqliteTableMetadata>> DiscoverTablesAsync()
    {
        EnsureConnected();
        var tables = new List<SqliteTableMetadata>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrationsHistory' ORDER BY name;";
        using var reader = await cmd.ExecuteReaderAsync();

        var tableNames = new List<string>();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }
        reader.Close();

        foreach (var tableName in tableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            var primaryKeys = columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();

            using var countCmd = _connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(1) FROM \"{tableName}\";";
            var rowCountObj = await countCmd.ExecuteScalarAsync();
            var rowCount = Convert.ToInt32(rowCountObj);

            tables.Add(new SqliteTableMetadata(tableName, rowCount, columns, primaryKeys));
        }

        return tables;
    }

    public async Task<List<SqliteColumnMetadata>> GetTableColumnsAsync(string tableName)
    {
        EnsureConnected();
        var columns = new List<SqliteColumnMetadata>();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var cid = reader.GetInt32(0);
            var name = reader.GetString(1);
            var type = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
            var notNull = reader.GetInt32(3) == 1;
            var dflt = reader.IsDBNull(4) ? null : reader.GetString(4);
            var pk = reader.GetInt32(5) > 0;

            columns.Add(new SqliteColumnMetadata(cid, name, type, notNull, dflt, pk));
        }

        return columns;
    }

    public async IAsyncEnumerable<Dictionary<string, object?>> StreamRowsAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{tableName}\";";
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
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("SQLite connection is not open. Call OpenAsync() first.");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}

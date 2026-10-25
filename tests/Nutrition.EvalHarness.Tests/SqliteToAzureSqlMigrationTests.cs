/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Nutrition.Tools.DataMigrator.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class SqliteToAzureSqlMigrationTests
{
    private readonly DataConversionEngine _engine = new();

    [Theory]
    [InlineData("2026-10-24 18:12:17", 2026, 10, 24, 18, 12, 17)]
    [InlineData("2026-10-24T18:12:17.1234567Z", 2026, 10, 24, 18, 12, 17)]
    [InlineData("2026-10-24T18:12:17Z", 2026, 10, 24, 18, 12, 17)]
    [InlineData("2026-10-24", 2026, 10, 24, 0, 0, 0)]
    public void DateTime_NormalizesVariousFormats_ToUtcDateTime(string input, int year, int month, int day, int hour, int min, int sec)
    {
        var stats = new TableConversionStats { TableName = "Meals" };
        var result = _engine.ConvertValue("Meals", "LoggedAt", "datetime2", false, null, input, "row-1", stats);

        Assert.NotNull(result);
        Assert.IsType<DateTime>(result);
        var dt = (DateTime)result!;
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(year, dt.Year);
        Assert.Equal(month, dt.Month);
        Assert.Equal(day, dt.Day);
        Assert.Equal(hour, dt.Hour);
        Assert.Equal(min, dt.Minute);
        Assert.Equal(sec, dt.Second);
        Assert.True(stats.DateTimeConversions > 0);
    }

    [Fact]
    public void DateTime_UnixEpochMilliseconds_ConvertsCorrectly()
    {
        var stats = new TableConversionStats { TableName = "AiUsageLogs" };
        // 1729800000000 ms = 2024-10-24 20:00:00 UTC
        var result = _engine.ConvertValue("AiUsageLogs", "TimestampUtc", "datetime2", false, null, "1729800000000", "row-1", stats);

        Assert.NotNull(result);
        Assert.IsType<DateTime>(result);
        var dt = (DateTime)result!;
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(10, dt.Month);
        Assert.True(stats.DateTimeConversions > 0);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    public void Boolean_ConvertsVariousSQLiteRepresentations_ToBit(object input, bool expected)
    {
        var stats = new TableConversionStats { TableName = "Users" };
        var result = _engine.ConvertValue("Users", "IsActive", "bit", false, null, input, "user-1", stats);

        Assert.NotNull(result);
        Assert.IsType<bool>(result);
        Assert.Equal(expected, (bool)result!);
    }

    [Fact]
    public void Json_RepairsEmptyOrCorruptedStrings_ToValidJsonArray()
    {
        var stats = new TableConversionStats { TableName = "Profiles" };

        // Empty string -> "[]"
        var emptyRes = _engine.ConvertValue("Profiles", "DiagnosedConditions", "nvarchar(max)", false, null, "", "row-1", stats);
        Assert.Equal("[]", emptyRes);

        // Comma-separated list -> valid json array ["Hypertension","Pre-Diabetes"]
        var csvRes = _engine.ConvertValue("Profiles", "DiagnosedConditions", "nvarchar(max)", false, null, "Hypertension,Pre-Diabetes", "row-2", stats);
        Assert.Equal("[\"Hypertension\",\"Pre-Diabetes\"]", csvRes);

        // Corrupted JSON -> "[]"
        var corruptRes = _engine.ConvertValue("Profiles", "DiagnosedConditions", "nvarchar(max)", false, null, "{not-valid-json", "row-3", stats);
        Assert.Equal("[]", corruptRes);

        Assert.True(stats.JsonConversions >= 3);
    }

    [Fact]
    public void Numeric_ReplacesNaNAndInfinity_WithZero()
    {
        var stats = new TableConversionStats { TableName = "Meals" };

        var nanRes = _engine.ConvertValue("Meals", "TotalCalories", "float", false, null, double.NaN, "row-1", stats);
        Assert.Equal(0.0, nanRes);

        var infRes = _engine.ConvertValue("Meals", "TotalCalories", "float", false, null, double.PositiveInfinity, "row-2", stats);
        Assert.Equal(0.0, nanRes);

        Assert.True(stats.NumericConversions >= 2);
    }

    [Theory]
    [InlineData("Role", "User", 0)]
    [InlineData("Role", "Admin", 1)]
    [InlineData("Role", "SuperAdmin", 2)]
    [InlineData("Tier", "Free", 0)]
    [InlineData("Tier", "Basic", 1)]
    [InlineData("Tier", "Premium", 2)]
    [InlineData("Tier", "SuperAdmin", 3)]
    [InlineData("MealType", "Breakfast", 0)]
    [InlineData("MealType", "Lunch", 2)]
    [InlineData("MealType", "Dinner", 4)]
    public void Enum_ResolvesStringToNumericCode(string columnName, string stringValue, int expectedCode)
    {
        var stats = new TableConversionStats { TableName = "Test" };
        var result = _engine.ConvertValue("Test", columnName, "int", false, null, stringValue, "row-1", stats);

        Assert.NotNull(result);
        Assert.Equal(expectedCode, Convert.ToInt32(result));
    }

    [Fact]
    public async Task SqliteSourceReader_DiscoversTablesAndStreamsRows()
    {
        SQLitePCL.Batteries.Init();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_source_{Guid.NewGuid():N}.db");

        try
        {
            // Seed a test SQLite database
            using (var conn = new SqliteConnection($"Data Source={tempFile}"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE TestItems (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Count INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );
                    INSERT INTO TestItems VALUES ('id-1', 'Apple', 10, 1, '2026-10-24 10:00:00');
                    INSERT INTO TestItems VALUES ('id-2', 'Banana', 20, 0, '2026-10-24 11:00:00');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            using var reader = new SqliteSourceReader(tempFile);
            await reader.OpenAsync();

            var tables = await reader.DiscoverTablesAsync();
            var testTable = Assert.Single(tables, t => t.TableName == "TestItems");

            Assert.Equal(2, testTable.RowCount);
            Assert.Contains("Id", testTable.PrimaryKeyColumns);
            Assert.Equal(5, testTable.Columns.Count);

            var rows = new List<Dictionary<string, object?>>();
            await foreach (var row in reader.StreamRowsAsync("TestItems"))
            {
                rows.Add(row);
            }

            Assert.Equal(2, rows.Count);
            Assert.Equal("Apple", rows[0]["Name"]?.ToString());
            Assert.Equal("Banana", rows[1]["Name"]?.ToString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
            }
        }
    }
}

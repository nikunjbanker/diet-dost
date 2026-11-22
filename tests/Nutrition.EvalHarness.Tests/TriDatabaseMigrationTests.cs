/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Bson;
using Nutrition.Tools.DataMigrator.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Unit and integration tests for the Tri-Database Migration Engine (SQLite <-> Azure SQL <-> DocumentDB).
/// Verifies BSON document conversion, JSON array expansion, UTC timestamp normalization, and round-trips.
/// </summary>
public class TriDatabaseMigrationTests
{
    private readonly DataConversionEngine _engine = new();

    [Fact]
    public void ConvertToBsonDocument_UserEntity_MapsIdToUnderscoreIdAndNormalizesUtcDates()
    {
        // Arrange
        var row = new Dictionary<string, object?>
        {
            ["Id"] = "usr_demo_123",
            ["Email"] = "user@dietdost.app",
            ["NormalizedEmail"] = "USER@DIETDOST.APP",
            ["IsActive"] = 1,
            ["IsDeleted"] = 0,
            ["CreatedAtUtc"] = "2026-11-20 14:30:00",
            ["DeletedAtUtc"] = null
        };
        var stats = new TableConversionStats { TableName = "Users" };

        // Act
        var bsonDoc = _engine.ConvertToBsonDocument("Users", row, "usr_demo_123", stats);

        // Assert
        Assert.NotNull(bsonDoc);
        Assert.Equal("usr_demo_123", bsonDoc["_id"].AsString);
        Assert.Equal("user@dietdost.app", bsonDoc["Email"].AsString);
        Assert.True(bsonDoc["IsActive"].AsBoolean);
        Assert.False(bsonDoc["IsDeleted"].AsBoolean);
        Assert.IsType<BsonDateTime>(bsonDoc["CreatedAtUtc"]);
        Assert.Equal(BsonNull.Value, bsonDoc["DeletedAtUtc"]);
        Assert.True(stats.TotalConversions > 0);
    }

    [Fact]
    public void ConvertToBsonDocument_KnownJsonColumns_ExpandsToJsonArray()
    {
        // Arrange - Meal with nested items
        var jsonItems = @"[
            {""Name"": ""Phulka"", ""Grams"": 60, ""Calories"": 70.0},
            {""Name"": ""Dal Tadka"", ""Grams"": 150, ""Calories"": 135.0}
        ]";

        var row = new Dictionary<string, object?>
        {
            ["Id"] = "meal_456",
            ["UserId"] = "usr_demo_123",
            ["DishName"] = "Dal Roti",
            ["Items"] = jsonItems,
            ["LoggedAt"] = "2026-11-20T12:00:00Z"
        };
        var stats = new TableConversionStats { TableName = "Meals" };

        // Act
        var bsonDoc = _engine.ConvertToBsonDocument("Meals", row, "meal_456", stats);

        // Assert
        Assert.NotNull(bsonDoc);
        Assert.Equal("meal_456", bsonDoc["_id"].AsString);
        Assert.IsType<BsonArray>(bsonDoc["Items"]);
        var array = bsonDoc["Items"].AsBsonArray;
        Assert.Equal(2, array.Count);
        Assert.Equal("Phulka", array[0]["Name"].AsString);
        Assert.Equal("Dal Tadka", array[1]["Name"].AsString);
    }

    [Fact]
    public void ConvertToBsonDocument_ProfileConditionsAndMedications_ExpandsArrays()
    {
        // Arrange
        var conditionsJson = @"[""Hypertension"", ""Pre-Diabetes""]";
        var medicationsJson = @"[{""DrugName"": ""Metformin 500mg"", ""Dosage"": ""500mg"", ""Frequency"": ""With Dinner""}]";

        var row = new Dictionary<string, object?>
        {
            ["Id"] = "usr_demo_123",
            ["Name"] = "Test User",
            ["DiagnosedConditions"] = conditionsJson,
            ["Medications"] = medicationsJson,
            ["CurrentWeightKg"] = 78.5
        };
        var stats = new TableConversionStats { TableName = "Profiles" };

        // Act
        var bsonDoc = _engine.ConvertToBsonDocument("Profiles", row, "usr_demo_123", stats);

        // Assert
        Assert.NotNull(bsonDoc);
        Assert.IsType<BsonArray>(bsonDoc["DiagnosedConditions"]);
        Assert.Equal(2, bsonDoc["DiagnosedConditions"].AsBsonArray.Count);
        Assert.IsType<BsonArray>(bsonDoc["Medications"]);
        Assert.Single(bsonDoc["Medications"].AsBsonArray);
        Assert.Equal(78.5, bsonDoc["CurrentWeightKg"].AsDouble);
    }

    [Fact]
    public void RoundTrip_SqliteRow_ToBson_ToNormalizedRow_PreservesDataIntegrity()
    {
        // Arrange
        var originalRow = new Dictionary<string, object?>
        {
            ["Id"] = "usr_roundtrip_789",
            ["Email"] = "roundtrip@dietdost.app",
            ["IsActive"] = 1,
            ["CreatedAtUtc"] = "2026-11-20T10:15:30Z",
            ["SecurityStamp"] = "abc123stamp"
        };
        var stats = new TableConversionStats { TableName = "Users" };

        // Step 1: Convert SQLite Row -> BSON Document
        var bsonDoc = _engine.ConvertToBsonDocument("Users", originalRow, "usr_roundtrip_789", stats);
        Assert.NotNull(bsonDoc);
        Assert.Equal("usr_roundtrip_789", bsonDoc["_id"].AsString);

        // Step 2: Convert BSON Document -> Normalized Row (simulating DocumentDbSourceReader)
        var reader = new DocumentDbSourceReader("mongodb://localhost:27017/dietdost_test");
        var normalizedRow = reader.ConvertBsonToRow(bsonDoc, "Users");

        // Assert
        Assert.Equal("usr_roundtrip_789", normalizedRow["Id"]);
        Assert.Equal("roundtrip@dietdost.app", normalizedRow["Email"]);
        Assert.Equal(true, normalizedRow["IsActive"]);
        Assert.NotNull(normalizedRow["CreatedAtUtc"]);
        var dtValue = Assert.IsType<DateTime>(normalizedRow["CreatedAtUtc"]);
        Assert.Equal(2026, dtValue.Year);
        Assert.Equal(11, dtValue.Month);
        Assert.Equal(20, dtValue.Day);

        // Step 3: Convert Normalized Row -> Azure SQL types
        var sqlActive = _engine.ConvertValue("Users", "IsActive", "bit", false, null, normalizedRow["IsActive"], "usr_roundtrip_789", stats);
        Assert.Equal(true, sqlActive);

        var sqlDate = _engine.ConvertValue("Users", "CreatedAtUtc", "datetime2", false, null, normalizedRow["CreatedAtUtc"], "usr_roundtrip_789", stats);
        Assert.IsType<DateTime>(sqlDate);
        var dt = (DateTime)sqlDate!;
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
        Assert.Equal(2026, dt.Year);
        Assert.Equal(11, dt.Month);
        Assert.Equal(20, dt.Day);
    }

    [Fact]
    public void DocumentDbSourceReader_DiscoversAllFourteenDomainTables()
    {
        var reader = new DocumentDbSourceReader("mongodb://localhost:27017/dietdost_test");

        // Use reflection to access the known collections list for verification
        var expectedCollections = new[]
        {
            "Users", "Profiles", "Meals", "FoodItems", "Ledgers",
            "ProgressPhotos", "Corrections", "AiFeedbacks", "TierConfigurations",
            "AiUsageLogs", "AppSecrets", "VerificationOtps",
            "QrSessions", "AppNotifications"
        };

        foreach (var coll in expectedCollections)
        {
            var isDomain = reader.IsDomainCollection(coll);
            Assert.True(isDomain, $"Expected collection '{coll}' to be recognized as a domain collection.");
        }

        // System collections should be ignored
        Assert.False(reader.IsDomainCollection("system.indexes"));
        Assert.False(reader.IsDomainCollection("system.views"));
    }

    [Fact]
    public void CommandLineParser_RecognizesAllPairwiseConnectionAliases()
    {
        // Arrange
        var args = new[]
        {
            "--source-type", "AzureSql",
            "--target-type", "DocumentDb",
            "--azure-sql-conn", "Server=tcp:sql-server.database.windows.net;Database=dietdost;",
            "--docdb-conn", "mongodb://docdb-cluster.mongocluster.cosmos.azure.com:27017/",
            "--mode", "Upsert",
            "--ensure-schema",
            "--dry-run"
        };

        // Act
        var parsed = Nutrition.Tools.DataMigrator.Configuration.MigrationCommandLineParser.Parse(args);
        var (source, target) = Nutrition.Tools.DataMigrator.Configuration.MigrationCommandLineParser.ResolveConnectionEndpoints(parsed);

        // Assert
        Assert.Equal(Nutrition.Tools.DataMigrator.DatabaseEngineType.AzureSql, parsed.SourceType);
        Assert.Equal(Nutrition.Tools.DataMigrator.DatabaseEngineType.DocumentDb, parsed.TargetType);
        Assert.Equal("Server=tcp:sql-server.database.windows.net;Database=dietdost;", source);
        Assert.Equal("mongodb://docdb-cluster.mongocluster.cosmos.azure.com:27017/", target);
        Assert.True(parsed.IsDryRun);
        Assert.True(parsed.EnsureSchema);
    }

    [Fact]
    public async Task MigrationReporter_GeneratesDynamicHeaderAndTableForAzureSqlToDocumentDb()
    {
        // Arrange
        var summary = new Nutrition.Tools.DataMigrator.Reporting.MigrationRunSummary(
            SourceDbPath: "Server=tcp:sql-server.database.windows.net;Database=dietdost;",
            TargetDatabaseName: "DocumentDb (Free Tier)",
            Mode: Nutrition.Tools.DataMigrator.MigrationMode.Upsert,
            IsDryRun: false,
            StartedAtUtc: new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc),
            CompletedAtUtc: new DateTime(2026, 11, 22, 15, 0, 10, DateTimeKind.Utc),
            TableResults:
            [
                new Nutrition.Tools.DataMigrator.Reporting.TableMigrationResult(
                    "Users", 19, 0, 19, 0, 0, 19, TimeSpan.FromSeconds(0.3),
                    new TableConversionStats { TableName = "Users" },
                    []
                )
            ],
            SourceEngine: "AzureSql",
            TargetEngine: "DocumentDb"
        );

        // Act
        var markdown = Nutrition.Tools.DataMigrator.Reporting.MigrationReporter.BuildMarkdownReport(summary);
        var reportPath = await Nutrition.Tools.DataMigrator.Reporting.MigrationReporter.GenerateMarkdownReportAsync(summary);
        var fileContent = await File.ReadAllTextAsync(reportPath);

        // Assert
        Assert.Contains("# AzureSql to DocumentDb Data Migration Report", markdown);
        Assert.Contains("AzureSql Source Rows", markdown);
        Assert.Contains("All source AzureSql records were successfully converted", markdown);
        Assert.Equal(markdown, fileContent);
    }
}

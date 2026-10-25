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

public enum MigrationMode
{
    Upsert,
    CleanInsert,
    SkipExisting
}

public record TargetColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    bool IsPrimaryKey,
    bool IsIdentity
);

public record TableMigrationResult(
    string TableName,
    int SourceRows,
    int TargetRowsBefore,
    int InsertedRows,
    int UpdatedRows,
    int SkippedRows,
    int TargetRowsAfter,
    TableConversionStats ConversionStats,
    List<string> Errors,
    TimeSpan Elapsed
);

public class AzureSqlTargetWriter : IDisposable
{
    private readonly string _connectionString;
    private SqlConnection? _connection;

    public AzureSqlTargetWriter(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task OpenAsync()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            ConnectTimeout = 30,
            Encrypt = true,
            TrustServerCertificate = false
        };

        _connection = new SqlConnection(builder.ConnectionString);
        await _connection.OpenAsync();
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName;";
        cmd.Parameters.AddWithValue("@tableName", tableName);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<int> GetRowCountAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM [{tableName}];";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<Dictionary<string, TargetColumnInfo>> GetTableColumnsAsync(string tableName)
    {
        EnsureConnected();
        var columns = new Dictionary<string, TargetColumnInfo>(StringComparer.OrdinalIgnoreCase);

        // 1. Column definitions
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName;";
        cmd.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase);
            int? maxLen = reader.IsDBNull(3) ? null : reader.GetInt32(3);

            columns[colName] = new TargetColumnInfo(colName, dataType, isNullable, maxLen, false, false);
        }
        reader.Close();

        // 2. Primary Keys
        using var pkCmd = _connection.CreateCommand();
        pkCmd.CommandText = @"
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
             AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = @tableName;";
        pkCmd.Parameters.AddWithValue("@tableName", tableName);

        using var pkReader = await pkCmd.ExecuteReaderAsync();
        while (await pkReader.ReadAsync())
        {
            var pkName = pkReader.GetString(0);
            if (columns.TryGetValue(pkName, out var col))
            {
                columns[pkName] = col with { IsPrimaryKey = true };
            }
        }
        pkReader.Close();

        // 3. Identity columns
        using var idCmd = _connection.CreateCommand();
        idCmd.CommandText = @"
            SELECT c.name FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = @tableName AND c.is_identity = 1;";
        idCmd.Parameters.AddWithValue("@tableName", tableName);

        using var idReader = await idCmd.ExecuteReaderAsync();
        while (await idReader.ReadAsync())
        {
            var idName = idReader.GetString(0);
            if (columns.TryGetValue(idName, out var col))
            {
                columns[idName] = col with { IsIdentity = true };
            }
        }
        idReader.Close();

        return columns;
    }

    public async Task DisableConstraintsAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"ALTER TABLE [{tableName}] NOCHECK CONSTRAINT ALL;";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task EnableConstraintsAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"ALTER TABLE [{tableName}] WITH CHECK CHECK CONSTRAINT ALL;";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task TruncateOrClearTableAsync(string tableName)
    {
        EnsureConnected();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{tableName}];";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task EnsureSchemaForTableAsync(string tableName)
    {
        EnsureConnected();
        if (await TableExistsAsync(tableName))
        {
            return;
        }

        var ddl = GetDdlForDomainTable(tableName);
        if (!string.IsNullOrWhiteSpace(ddl))
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<TableMigrationResult> MigrateTableAsync(
        SqliteTableMetadata sourceTable,
        IAsyncEnumerable<Dictionary<string, object?>> sourceRows,
        DataConversionEngine engine,
        MigrationMode mode,
        bool isDryRun,
        Action<int>? onRowProgress = null)
    {
        EnsureConnected();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var errors = new List<string>();
        var conversionStats = new TableConversionStats { TableName = sourceTable.TableName };

        var targetCols = await GetTableColumnsAsync(sourceTable.TableName);
        if (targetCols.Count == 0)
        {
            errors.Add($"Target table [{sourceTable.TableName}] does not exist in Azure SQL and could not be resolved.");
            return new TableMigrationResult(sourceTable.TableName, sourceTable.RowCount, 0, 0, 0, 0, 0, conversionStats, errors, sw.Elapsed);
        }

        var targetRowsBefore = await GetRowCountAsync(sourceTable.TableName);
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        // If CleanInsert mode and not dry run, clear target table first
        if (mode == MigrationMode.CleanInsert && !isDryRun)
        {
            await TruncateOrClearTableAsync(sourceTable.TableName);
        }

        var pkColumns = targetCols.Values.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        if (pkColumns.Count == 0 && sourceTable.PrimaryKeyColumns.Count > 0)
        {
            pkColumns = sourceTable.PrimaryKeyColumns;
        }

        var hasIdentity = targetCols.Values.Any(c => c.IsIdentity);

        if (!isDryRun)
        {
            await DisableConstraintsAsync(sourceTable.TableName);
            if (hasIdentity)
            {
                using var idOn = _connection!.CreateCommand();
                idOn.CommandText = $"SET IDENTITY_INSERT [{sourceTable.TableName}] ON;";
                await idOn.ExecuteNonQueryAsync();
            }
        }

        int processedInBatch = 0;
        using var tx = !isDryRun ? _connection!.BeginTransaction() : null;

        try
        {
            await foreach (var row in sourceRows)
            {
                conversionStats.TotalRowsRead++;
                var rowKey = GetRowKeyString(row, pkColumns);

                // Convert columns
                var convertedRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (colName, colMeta) in targetCols)
                {
                    row.TryGetValue(colName, out var rawVal);
                    try
                    {
                        var converted = engine.ConvertValue(
                            sourceTable.TableName,
                            colName,
                            colMeta.DataType,
                            colMeta.IsNullable,
                            colMeta.MaxLength,
                            rawVal,
                            rowKey,
                            conversionStats);
                        convertedRow[colName] = converted ?? DBNull.Value;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row '{rowKey}' Col '{colName}': Type conversion failed: {ex.Message}");
                        convertedRow[colName] = colMeta.IsNullable ? DBNull.Value : string.Empty;
                    }
                }

                if (!isDryRun)
                {
                    try
                    {
                        var action = await ExecuteMergeOrInsertRowAsync(sourceTable.TableName, convertedRow, targetCols, pkColumns, mode, tx!);
                        if (action == "INSERT") inserted++;
                        else if (action == "UPDATE") updated++;
                        else skipped++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row '{rowKey}': Database write failed ({ex.GetType().Name}): {ex.Message}");
                    }
                }
                else
                {
                    inserted++;
                }

                processedInBatch++;
                if (processedInBatch % 100 == 0)
                {
                    onRowProgress?.Invoke(processedInBatch);
                }
            }

            if (!isDryRun && tx != null)
            {
                await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            if (!isDryRun && tx != null)
            {
                await tx.RollbackAsync();
            }
            errors.Add($"Transaction failed for table [{sourceTable.TableName}]: {ex.Message}");
        }
        finally
        {
            if (!isDryRun)
            {
                if (hasIdentity)
                {
                    using var idOff = _connection!.CreateCommand();
                    idOff.CommandText = $"SET IDENTITY_INSERT [{sourceTable.TableName}] OFF;";
                    await idOff.ExecuteNonQueryAsync();
                }
                await EnableConstraintsAsync(sourceTable.TableName);
            }
        }

        var targetRowsAfter = !isDryRun ? await GetRowCountAsync(sourceTable.TableName) : targetRowsBefore + inserted;
        sw.Stop();

        return new TableMigrationResult(
            sourceTable.TableName,
            sourceTable.RowCount,
            targetRowsBefore,
            inserted,
            updated,
            skipped,
            targetRowsAfter,
            conversionStats,
            errors,
            sw.Elapsed);
    }

    private async Task<string> ExecuteMergeOrInsertRowAsync(
        string tableName,
        Dictionary<string, object?> row,
        Dictionary<string, TargetColumnInfo> targetCols,
        List<string> pkColumns,
        MigrationMode mode,
        SqlTransaction tx)
    {
        var colNames = targetCols.Keys.ToList();

        if (pkColumns.Count == 0 || mode == MigrationMode.CleanInsert)
        {
            // Simple Insert
            var insertCols = string.Join(", ", colNames.Select(c => $"[{c}]"));
            var insertParams = string.Join(", ", colNames.Select((c, i) => $"@p{i}"));
            var insertSql = $"INSERT INTO [{tableName}] ({insertCols}) VALUES ({insertParams});";

            using var cmd = _connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = insertSql;
            for (int i = 0; i < colNames.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", row[colNames[i]] ?? DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync();
            return "INSERT";
        }

        // Check if row already exists
        var checkWhere = string.Join(" AND ", pkColumns.Select((c, i) => $"[{c}] = @pk{i}"));
        var checkSql = $"SELECT COUNT(1) FROM [{tableName}] WHERE {checkWhere};";

        using var checkCmd = _connection!.CreateCommand();
        checkCmd.Transaction = tx;
        checkCmd.CommandText = checkSql;
        for (int i = 0; i < pkColumns.Count; i++)
        {
            checkCmd.Parameters.AddWithValue($"@pk{i}", row[pkColumns[i]] ?? DBNull.Value);
        }
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

        if (exists)
        {
            if (mode == MigrationMode.SkipExisting)
            {
                return "SKIP";
            }

            // Mode == Upsert -> Update non-PK columns
            var nonPkCols = colNames.Where(c => !pkColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (nonPkCols.Count == 0)
            {
                return "SKIP";
            }

            var setClause = string.Join(", ", nonPkCols.Select((c, i) => $"[{c}] = @val{i}"));
            var updateSql = $"UPDATE [{tableName}] SET {setClause} WHERE {checkWhere};";

            using var updateCmd = _connection.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = updateSql;
            for (int i = 0; i < nonPkCols.Count; i++)
            {
                updateCmd.Parameters.AddWithValue($"@val{i}", row[nonPkCols[i]] ?? DBNull.Value);
            }
            for (int i = 0; i < pkColumns.Count; i++)
            {
                updateCmd.Parameters.AddWithValue($"@pk{i}", row[pkColumns[i]] ?? DBNull.Value);
            }
            await updateCmd.ExecuteNonQueryAsync();
            return "UPDATE";
        }
        else
        {
            // Insert
            var insertCols = string.Join(", ", colNames.Select(c => $"[{c}]"));
            var insertParams = string.Join(", ", colNames.Select((c, i) => $"@p{i}"));
            var insertSql = $"INSERT INTO [{tableName}] ({insertCols}) VALUES ({insertParams});";

            using var insertCmd = _connection.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = insertSql;
            for (int i = 0; i < colNames.Count; i++)
            {
                insertCmd.Parameters.AddWithValue($"@p{i}", row[colNames[i]] ?? DBNull.Value);
            }
            await insertCmd.ExecuteNonQueryAsync();
            return "INSERT";
        }
    }

    private static string GetRowKeyString(Dictionary<string, object?> row, List<string> pkColumns)
    {
        if (pkColumns.Count > 0)
        {
            var keys = pkColumns.Select(pk => row.TryGetValue(pk, out var v) ? v?.ToString() ?? "NULL" : "NULL");
            return string.Join("|", keys);
        }
        if (row.TryGetValue("Id", out var idVal) && idVal != null)
        {
            return idVal.ToString()!;
        }
        if (row.TryGetValue("Key", out var keyVal) && keyVal != null)
        {
            return keyVal.ToString()!;
        }
        return Guid.NewGuid().ToString("N")[..8];
    }

    private static string? GetDdlForDomainTable(string tableName)
    {
        return tableName.ToLowerInvariant() switch
        {
            "users" => @"
                CREATE TABLE [Users] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
                    [Email] NVARCHAR(256) NOT NULL,
                    [NormalizedEmail] NVARCHAR(256) NOT NULL,
                    [MobileNumber] NVARCHAR(50) NOT NULL,
                    [NormalizedMobileNumber] NVARCHAR(50) NOT NULL,
                    [PasswordHash] NVARCHAR(MAX) NOT NULL,
                    [SecurityStamp] NVARCHAR(MAX) NOT NULL,
                    [Role] INT NOT NULL,
                    [Tier] INT NOT NULL,
                    [IsEmailVerified] BIT NOT NULL,
                    [IsMobileVerified] BIT NOT NULL,
                    [IsActive] BIT NOT NULL,
                    [TermsAcceptedAtUtc] DATETIME2 NULL,
                    [TermsVersionAccepted] NVARCHAR(MAX) NULL,
                    [HealthConsentAcceptedAtUtc] DATETIME2 NULL,
                    [HealthConsentVersionAccepted] NVARCHAR(MAX) NULL,
                    [ConsentIpAddress] NVARCHAR(MAX) NULL,
                    [ConsentUserAgent] NVARCHAR(MAX) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [LastLoginAtUtc] DATETIME2 NULL
                );",
            "profiles" => @"
                CREATE TABLE [Profiles] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Profiles] PRIMARY KEY,
                    [Name] NVARCHAR(MAX) NOT NULL,
                    [Sex] INT NOT NULL,
                    [Age] INT NOT NULL,
                    [HeightCm] FLOAT NOT NULL,
                    [CurrentWeightKg] FLOAT NOT NULL,
                    [TargetWeightKg] FLOAT NOT NULL,
                    [DesiredPaceKgPerWeek] FLOAT NOT NULL,
                    [ActivityLevel] INT NOT NULL,
                    [DietaryPreference] INT NOT NULL,
                    [RegionalCuisine] NVARCHAR(MAX) NOT NULL,
                    [Timezone] NVARCHAR(MAX) NOT NULL,
                    [DiagnosedConditions] NVARCHAR(MAX) NOT NULL,
                    [Medications] NVARCHAR(MAX) NOT NULL
                );",
            "meals" => @"
                CREATE TABLE [Meals] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Meals] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [LoggedAt] DATETIME2 NOT NULL,
                    [MealType] INT NOT NULL,
                    [DishName] NVARCHAR(MAX) NOT NULL,
                    [PhotoUri] NVARCHAR(MAX) NULL,
                    [OverallConfidenceScore] FLOAT NOT NULL,
                    [IsVerifiedByUser] BIT NOT NULL,
                    [AddedGheeKcal] FLOAT NOT NULL,
                    [AddedTadkaKcal] FLOAT NOT NULL,
                    [DietitianAdvice] NVARCHAR(MAX) NULL,
                    [WhoComplianceFlags] NVARCHAR(MAX) NOT NULL,
                    [MedicationWarnings] NVARCHAR(MAX) NOT NULL,
                    [TotalCalories] FLOAT NOT NULL DEFAULT 0,
                    [TotalProteinGrams] FLOAT NOT NULL DEFAULT 0,
                    [TotalCarbsGrams] FLOAT NOT NULL DEFAULT 0,
                    [TotalFatGrams] FLOAT NOT NULL DEFAULT 0,
                    [TotalFiberGrams] FLOAT NOT NULL DEFAULT 0,
                    [TotalSugarGrams] FLOAT NOT NULL DEFAULT 0,
                    [TotalSodiumMg] FLOAT NOT NULL DEFAULT 0,
                    [AiFeedbackRating] NVARCHAR(MAX) NULL,
                    [AiFeedbackRemarks] NVARCHAR(MAX) NULL,
                    [ClientMutationId] NVARCHAR(450) NULL,
                    [ClientTimestampUtc] NVARCHAR(MAX) NULL
                );",
            "fooditems" => @"
                CREATE TABLE [FoodItems] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_FoodItems] PRIMARY KEY,
                    [MealLogId] NVARCHAR(450) NOT NULL,
                    [Name] NVARCHAR(MAX) NOT NULL,
                    [HindiOrRegionalName] NVARCHAR(MAX) NOT NULL,
                    [EstimatedPortion] NVARCHAR(MAX) NOT NULL,
                    [Quantity] FLOAT NOT NULL,
                    [Grams] FLOAT NOT NULL,
                    [Calories] FLOAT NOT NULL,
                    [ProteinGrams] FLOAT NOT NULL,
                    [CarbsGrams] FLOAT NOT NULL,
                    [FatGrams] FLOAT NOT NULL,
                    [FiberGrams] FLOAT NOT NULL DEFAULT 0,
                    [SugarGrams] FLOAT NOT NULL DEFAULT 0,
                    [SodiumMg] FLOAT NOT NULL DEFAULT 0,
                    [CookingMediumEstimate] NVARCHAR(MAX) NOT NULL,
                    [ConfidenceScore] FLOAT NOT NULL,
                    [OriginalDetection] NVARCHAR(MAX) NOT NULL DEFAULT ''
                );",
            "ledgers" => @"
                CREATE TABLE [Ledgers] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Ledgers] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [Date] DATETIME2 NOT NULL,
                    [TargetCalories] FLOAT NOT NULL,
                    [ConsumedCalories] FLOAT NOT NULL,
                    [TargetProteinGrams] FLOAT NOT NULL,
                    [ConsumedProteinGrams] FLOAT NOT NULL,
                    [TargetCarbsGrams] FLOAT NOT NULL,
                    [ConsumedCarbsGrams] FLOAT NOT NULL,
                    [TargetFatGrams] FLOAT NOT NULL,
                    [ConsumedFatGrams] FLOAT NOT NULL,
                    [TargetFiberGrams] FLOAT NOT NULL,
                    [ConsumedFiberGrams] FLOAT NOT NULL,
                    [TargetSugarGrams] FLOAT NOT NULL DEFAULT 25.0,
                    [ConsumedSugarGrams] FLOAT NOT NULL DEFAULT 0.0,
                    [TargetSodiumMg] FLOAT NOT NULL,
                    [ConsumedSodiumMg] FLOAT NOT NULL,
                    [EarnedBadges] NVARCHAR(MAX) NOT NULL,
                    [LastUpdatedUtc] DATETIME2 NOT NULL
                );",
            "corrections" => @"
                CREATE TABLE [Corrections] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Corrections] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [OriginalDetectedItem] NVARCHAR(450) NOT NULL,
                    [CorrectedItemName] NVARCHAR(MAX) NOT NULL,
                    [HindiOrRegionalName] NVARCHAR(MAX) NOT NULL,
                    [EstimatedPortion] NVARCHAR(MAX) NOT NULL,
                    [Calories] FLOAT NOT NULL,
                    [ProteinGrams] FLOAT NOT NULL,
                    [CarbsGrams] FLOAT NOT NULL,
                    [FatGrams] FLOAT NOT NULL,
                    [MealType] NVARCHAR(MAX) NOT NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [FrequencyCount] INT NOT NULL
                );",
            "progressphotos" => @"
                CREATE TABLE [ProgressPhotos] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_ProgressPhotos] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [CapturedAtUtc] DATETIME2 NOT NULL,
                    [WeightKg] FLOAT NOT NULL,
                    [PhotoType] INT NOT NULL,
                    [PhotoUri] NVARCHAR(MAX) NOT NULL,
                    [IsBaseline] BIT NOT NULL,
                    [Notes] NVARCHAR(MAX) NULL
                );",
            "aifeedbacks" => @"
                CREATE TABLE [AiFeedbacks] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_AiFeedbacks] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [MealLogId] NVARCHAR(450) NULL,
                    [DishName] NVARCHAR(MAX) NOT NULL,
                    [DetectedByModel] NVARCHAR(MAX) NOT NULL,
                    [ConfidenceScore] FLOAT NOT NULL,
                    [Rating] NVARCHAR(450) NOT NULL,
                    [Remarks] NVARCHAR(MAX) NULL,
                    [IdentifiedItemsSummary] NVARCHAR(MAX) NULL,
                    [RetrainingTriggered] BIT NOT NULL,
                    [RetrainingOutcome] NVARCHAR(MAX) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL
                );",
            "tierconfigurations" => @"
                CREATE TABLE [TierConfigurations] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_TierConfigurations] PRIMARY KEY,
                    [Tier] INT NOT NULL,
                    [DailyAiDetectionLimit] INT NOT NULL,
                    [AllowPhotoCompare] BIT NOT NULL,
                    [AllowDataExport] BIT NOT NULL,
                    [AnalyticsHistoryDays] INT NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedByUserId] NVARCHAR(450) NULL
                );",
            "aiusagelogs" => @"
                CREATE TABLE [AiUsageLogs] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_AiUsageLogs] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [OperationType] INT NOT NULL,
                    [ModelId] NVARCHAR(MAX) NOT NULL,
                    [EstimatedTokensUsed] INT NOT NULL,
                    [LatencyMs] FLOAT NOT NULL,
                    [IsSuccess] BIT NOT NULL,
                    [ErrorReason] NVARCHAR(MAX) NULL,
                    [TimestampUtc] DATETIME2 NOT NULL
                );",
            "appsecrets" => @"
                CREATE TABLE [AppSecrets] (
                    [Key] NVARCHAR(128) NOT NULL CONSTRAINT [PK_AppSecrets] PRIMARY KEY,
                    [Value] NVARCHAR(MAX) NOT NULL,
                    [Description] NVARCHAR(512) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );",
            "verificationotps" => @"
                CREATE TABLE [VerificationOtps] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_VerificationOtps] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [Target] NVARCHAR(450) NOT NULL,
                    [OtpCodeHash] NVARCHAR(MAX) NOT NULL,
                    [Channel] INT NOT NULL,
                    [ExpiresAtUtc] DATETIME2 NOT NULL,
                    [AttemptCount] INT NOT NULL,
                    [IsUsed] BIT NOT NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL
                );",
            _ => null
        };
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

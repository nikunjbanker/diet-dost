/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Encapsulates SQLite database schema verification, PRAGMA migrations,
/// and deterministic seeding of tier policies and demo accounts.
/// </summary>
public static partial class DatabaseInitializationExtensions
{
    private static readonly string[] ScriptSeparators = [";\r\n", ";\n"];

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial System.Text.RegularExpressions.Regex ValidIdentifierRegex();

    public static async Task InitializeAndSeedDatabaseAsync(this IApplicationBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

        var appEnv = scope.ServiceProvider.GetRequiredService<Nutrition.Application.Common.Interfaces.IAppEnvironment>();

        try
        {
            if (db.Database.IsSqlite())
            {
                var sqliteRetryPipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions
                    {
                        ShouldHandle = new PredicateBuilder().Handle<SqliteException>(ex => ex.SqliteErrorCode == 5),
                        MaxRetryAttempts = 5,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(2),
                        MaxDelay = TimeSpan.FromSeconds(30),
                        OnRetry = args =>
                        {
                            if (logger.IsEnabled(LogLevel.Warning))
                            {
                                var attempt = args.AttemptNumber + 1;
                                var delaySeconds = (int)args.RetryDelay.TotalSeconds;
                                logger.LogWarning(args.Outcome.Exception, "SQLite database is temporarily locked (attempt {Attempt}/5). Waiting {DelaySeconds}s for lock release on SMB share...", attempt, delaySeconds);
                            }
                            return ValueTask.CompletedTask;
                        }
                    })
                    .Build();

                await sqliteRetryPipeline.ExecuteAsync(async _ => await EnsureDatabaseCreatedAndMigratedAsync(db, logger));
            }
            else if (db.Database.IsSqlServer())
            {
                var sqlServerRetryPipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions
                    {
                        ShouldHandle = new PredicateBuilder()
                            .Handle<SqlException>(ex =>
                            {
                                if (ex.Number == 42119)
                                {
                                    logger.LogCritical(ex, "[AZURE SQL QUOTA EXHAUSTED] Azure SQL Database free tier allowance (100,000 vCore-seconds) is exhausted (Error 42119). Azure auto-paused this database. Run 'az sql db update --free-limit-exhaustion-behavior BillOverUsage' or set 'Database__Provider=Sqlite'.");
                                    return false;
                                }
                                return ex.Number == 40613 ||
                                       ex.Number == 258 ||
                                       ex.Number == -2 ||
                                       ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                                       ex.Message.Contains("available", StringComparison.OrdinalIgnoreCase);
                            })
                            .Handle<TimeoutException>(),
                        MaxRetryAttempts = 6,
                        BackoffType = DelayBackoffType.Constant,
                        Delay = TimeSpan.FromSeconds(10),
                        OnRetry = args =>
                        {
                            if (logger.IsEnabled(LogLevel.Warning))
                            {
                                var attempt = args.AttemptNumber + 1;
                                var delaySeconds = (int)args.RetryDelay.TotalSeconds;
                                logger.LogWarning(args.Outcome.Exception, "[WARMUP] Azure SQL Serverless database is auto-resuming from cold pause (attempt {Attempt}/6). Waiting {DelaySeconds}s for instance to become online...", attempt, delaySeconds);
                            }
                            return ValueTask.CompletedTask;
                        }
                    })
                    .Build();

                await sqlServerRetryPipeline.ExecuteAsync(async _ => await EnsureDatabaseCreatedAndMigratedAsync(db, logger));
            }
            else
            {
                await EnsureDatabaseCreatedAndMigratedAsync(db, logger);
            }

            await SeedAppSecretsAsync(db, configuration, appEnv, logger);
            await SeedTierConfigurationsAsync(db, logger);
            await EnsureSuperAdminAccountAsync(db, scope.ServiceProvider, configuration, logger);

            if (appEnv.AllowsDemoUsers)
            {
                await SeedDemoUsersAsync(db, scope.ServiceProvider, configuration, appEnv, logger);
            }
            else
            {
                logger.LogWarning("[SECURITY] Release mode or non-Development environment detected. Demo user seeding skipped and existing demo accounts deactivated.");
                await DeactivateDemoUsersInReleaseModeAsync(db, logger);
            }

            await MigrateLegacyDataAsync(db, logger);

            var vaultSyncService = scope.ServiceProvider.GetService<Nutrition.Application.Common.Interfaces.ISqliteStorageVaultSyncService>();
            if (vaultSyncService != null && vaultSyncService.IsEnabled)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("[INIT] Synchronizing pristine seeded database to durable storage vault '{VaultPath}'...", vaultSyncService.StorageVaultPath);
                }
                await vaultSyncService.SyncActiveToVaultAsync();
            }

            EnsureSeedMediaAssetsMirrored(scope.ServiceProvider, logger);

            if (logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CA1873 // Avoid evaluation of arguments when logging is disabled
                logger.LogInformation("Database schema verified and initialized successfully with 0 errors using provider '{Provider}'.", db.Database.ProviderName);
#pragma warning restore CA1873
            }
        }
        catch (Exception ex)
        {
            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 42119)
            {
                logger.LogCritical(ex, "[AZURE SQL QUOTA EXHAUSTED] Azure SQL Database free monthly quota (100,000 vCore-seconds) was reached (Error 42119). Set Database__Provider=Sqlite or enable BillOverUsage.");
            }
            else if (logger.IsEnabled(LogLevel.Error))
            {
#pragma warning disable CA1873 // Avoid evaluation of arguments when logging is disabled
                logger.LogError(ex, "Critical database initialization failure during startup. ErrorType: {ErrorType}, Message: {ErrorMessage}", ex.GetType().Name, ex.Message);
#pragma warning restore CA1873
            }
            throw;
        }
    }

    public static async Task EnsureDatabaseCreatedAndMigratedAsync(DietTrackerDbContext db, ILogger logger)
    {
        if (db.Database.IsSqlServer())
        {
            await EnsureSqlServerDatabaseMigratedAsync(db, logger);
            return;
        }

        var connection = db.Database.GetDbConnection();

        // Fast Pristine Schema Bootstrapping:
        // Azure Files SMB / CIFS mounts have high per-operation latency (~800ms) and do not support
        // POSIX byte-range locking required by SQLite transactions. Executing 140+ DDL statements
        // over SMB takes >120s and transaction COMMIT triggers SQLITE_BUSY (error 5).
        // If the target SQLite database is missing or empty (0 bytes), we bootstrap the pristine
        // schema in local container temporary storage (/tmp) in <50ms, switch to DELETE journal mode,
        // and copy the single complete database file to the target volume in a single atomic I/O operation.
        if (connection is SqliteConnection sqliteConn)
        {
            var builder = new SqliteConnectionStringBuilder(sqliteConn.ConnectionString);
            var dbPath = builder.DataSource;

            if (!string.IsNullOrWhiteSpace(dbPath) && !dbPath.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(dbPath);
                var fi = new FileInfo(fullPath);
                if (!fi.Exists || fi.Length == 0)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("[INIT] Pristine SQLite bootstrapping initiated for '{DbPath}' via local container temporary storage...", fullPath);
                    }
                    var tempFile = Path.Combine(Path.GetTempPath(), $"diet_dost_init_{Guid.NewGuid():N}.db");
                    try
                    {
                        var tempOptions = new DbContextOptionsBuilder<DietTrackerDbContext>()
                            .UseSqlite($"Data Source={tempFile}")
                            .Options;

                        using (var tempDb = new DietTrackerDbContext(tempOptions))
                        {
                            await tempDb.Database.EnsureCreatedAsync();
                            // Checkpoint any WAL entries and ensure journal mode is DELETE for SMB CIFS compatibility
                            await tempDb.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode = 'DELETE';");
                        }

                        var targetDir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(tempFile, fullPath, overwrite: true);
                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation("[INIT] Pristine SQLite schema successfully deployed to '{DbPath}'.", fullPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[INIT] Local pristine schema bootstrap encountered an exception; falling back to in-place creation.");
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                        {
                            try { File.Delete(tempFile); } catch { }
                        }
                    }
                }
            }
        }

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Check if core domain tables already exist.
        var mealsExist = await TableExistsAsync(db, "Meals");
        if (!mealsExist)
        {
            var createScript = db.Database.GenerateCreateScript();
            var rawStatements = createScript.Split(ScriptSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            using var createCmd = connection.CreateCommand();
            foreach (var rawStatement in rawStatements)
            {
                if (string.IsNullOrWhiteSpace(rawStatement)) continue;
                try
                {
#pragma warning disable CA2100 // Raw statement originates directly from internally generated EF Core DbContext CreateScript and is sanitized
                    createCmd.CommandText = rawStatement;
#pragma warning restore CA2100
                    await createCmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Table, index, or constraint may already exist
                }
            }
            return;
        }

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Corrections"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Corrections"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""OriginalDetectedItem"" TEXT NOT NULL,
                ""CorrectedItemName"" TEXT NOT NULL,
                ""HindiOrRegionalName"" TEXT NOT NULL,
                ""EstimatedPortion"" TEXT NOT NULL,
                ""Calories"" REAL NOT NULL,
                ""ProteinGrams"" REAL NOT NULL,
                ""CarbsGrams"" REAL NOT NULL,
                ""FatGrams"" REAL NOT NULL,
                ""MealType"" TEXT NOT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""FrequencyCount"" INTEGER NOT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""DeletedAtUtc"" TEXT NULL
            );");

        await EnsureColumnExistsAsync(db, "Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRating", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRemarks", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Meals", "ClientMutationId", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Meals", "ClientTimestampUtc", "TEXT NULL");
        await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Meals_UserId_ClientMutationId"" ON ""Meals"" (""UserId"", ""ClientMutationId"");");
        await EnsureColumnExistsAsync(db, "FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnExistsAsync(db, "FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0");
        await EnsureColumnExistsAsync(db, "Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0");
        await EnsureColumnExistsAsync(db, "Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'");
        string boolColDef = db.Database.IsSqlServer() ? "BIT NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0";
        await EnsureColumnExistsAsync(db, "Profiles", "IsOnboarded", boolColDef);
        await EnsureColumnExistsAsync(db, "Users", "IsOnboarded", boolColDef);
        await EnsureSoftDeleteColumnsAsync(db);

        // Reconcile Users table columns for legacy SQLite installations
        await EnsureColumnExistsAsync(db, "Users", "IsAnonymous", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Users", "DisplayName", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryLookupId", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryKeyHash", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoverySalt", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryKeyCreatedAtUtc", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "TermsAcceptedAtUtc", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "TermsVersionAccepted", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "HealthConsentAcceptedAtUtc", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "HealthConsentVersionAccepted", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "ConsentIpAddress", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "ConsentUserAgent", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "LastLoginAtUtc", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Users", "LastActiveUtc", "TEXT NULL");
        await EnsureIndexExistsAsync(db, "Users", "IX_Users_LastActiveUtc", "\"LastActiveUtc\"");
        await EnsureIndexExistsAsync(db, "Users", "IX_Users_RecoveryLookupId", "\"RecoveryLookupId\"");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""ProgressPhotos"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ProgressPhotos"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""CapturedAtUtc"" TEXT NOT NULL,
                ""WeightKg"" REAL NOT NULL,
                ""PhotoType"" INTEGER NOT NULL,
                ""PhotoUri"" TEXT NOT NULL,
                ""IsBaseline"" INTEGER NOT NULL,
                ""Notes"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""DeletedAtUtc"" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_ProgressPhotos_UserId_CapturedAtUtc"" ON ""ProgressPhotos"" (""UserId"", ""CapturedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_ProgressPhotos_UserId_PhotoType"" ON ""ProgressPhotos"" (""UserId"", ""PhotoType"");

            CREATE TABLE IF NOT EXISTS ""AiFeedbacks"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AiFeedbacks"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""MealLogId"" TEXT NULL,
                ""DishName"" TEXT NOT NULL,
                ""DetectedByModel"" TEXT NOT NULL,
                ""ConfidenceScore"" REAL NOT NULL,
                ""Rating"" TEXT NOT NULL,
                ""Remarks"" TEXT NULL,
                ""IdentifiedItemsSummary"" TEXT NULL,
                ""RetrainingTriggered"" INTEGER NOT NULL,
                ""RetrainingOutcome"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""DeletedAtUtc"" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AiFeedbacks_UserId_CreatedAtUtc"" ON ""AiFeedbacks"" (""UserId"", ""CreatedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_AiFeedbacks_Rating"" ON ""AiFeedbacks"" (""Rating"");

            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY,
                ""Email"" TEXT NOT NULL,
                ""NormalizedEmail"" TEXT NOT NULL,
                ""MobileNumber"" TEXT NOT NULL,
                ""NormalizedMobileNumber"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NOT NULL,
                ""SecurityStamp"" TEXT NOT NULL,
                ""Role"" INTEGER NOT NULL,
                ""Tier"" INTEGER NOT NULL,
                ""IsEmailVerified"" INTEGER NOT NULL,
                ""IsMobileVerified"" INTEGER NOT NULL,
                ""IsActive"" INTEGER NOT NULL,
                ""TermsAcceptedAtUtc"" TEXT NULL,
                ""TermsVersionAccepted"" TEXT NULL,
                ""HealthConsentAcceptedAtUtc"" TEXT NULL,
                ""HealthConsentVersionAccepted"" TEXT NULL,
                ""ConsentIpAddress"" TEXT NULL,
                ""ConsentUserAgent"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""LastLoginAtUtc"" TEXT NULL,
                ""IsAnonymous"" INTEGER NOT NULL DEFAULT 0,
                ""DisplayName"" TEXT NULL,
                ""RecoveryLookupId"" TEXT NULL,
                ""RecoveryKeyHash"" TEXT NULL,
                ""RecoverySalt"" TEXT NULL,
                ""RecoveryKeyCreatedAtUtc"" TEXT NULL,
                ""IsOnboarded"" INTEGER NOT NULL DEFAULT 0,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""DeletedAtUtc"" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_NormalizedEmail"" ON ""Users"" (""NormalizedEmail"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_NormalizedMobileNumber"" ON ""Users"" (""NormalizedMobileNumber"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_Role"" ON ""Users"" (""Role"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_Tier"" ON ""Users"" (""Tier"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_RecoveryLookupId"" ON ""Users"" (""RecoveryLookupId"");

            CREATE TABLE IF NOT EXISTS ""VerificationOtps"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_VerificationOtps"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""Target"" TEXT NOT NULL,
                ""OtpCodeHash"" TEXT NOT NULL,
                ""Channel"" INTEGER NOT NULL,
                ""ExpiresAtUtc"" TEXT NOT NULL,
                ""AttemptCount"" INTEGER NOT NULL,
                ""IsUsed"" INTEGER NOT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_UserId_Target"" ON ""VerificationOtps"" (""UserId"", ""Target"");
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_Target_Channel_IsUsed"" ON ""VerificationOtps"" (""Target"", ""Channel"", ""IsUsed"");
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_ExpiresAtUtc"" ON ""VerificationOtps"" (""ExpiresAtUtc"");

            CREATE TABLE IF NOT EXISTS ""TierConfigurations"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TierConfigurations"" PRIMARY KEY,
                ""Tier"" INTEGER NOT NULL,
                ""DailyAiDetectionLimit"" INTEGER NOT NULL,
                ""AllowPhotoCompare"" INTEGER NOT NULL,
                ""AllowDataExport"" INTEGER NOT NULL,
                ""AnalyticsHistoryDays"" INTEGER NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                ""UpdatedByUserId"" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TierConfigurations_Tier"" ON ""TierConfigurations"" (""Tier"");

            CREATE TABLE IF NOT EXISTS ""AiUsageLogs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AiUsageLogs"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""OperationType"" INTEGER NOT NULL,
                ""ModelId"" TEXT NOT NULL,
                ""EstimatedTokensUsed"" INTEGER NOT NULL,
                ""LatencyMs"" REAL NOT NULL,
                ""IsSuccess"" INTEGER NOT NULL,
                ""ErrorReason"" TEXT NULL,
                ""TimestampUtc"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AiUsageLogs_UserId_TimestampUtc"" ON ""AiUsageLogs"" (""UserId"", ""TimestampUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_AiUsageLogs_UserId_OperationType"" ON ""AiUsageLogs"" (""UserId"", ""OperationType"");

            CREATE TABLE IF NOT EXISTS ""AppSecrets"" (
                ""Key"" TEXT NOT NULL CONSTRAINT ""PK_AppSecrets"" PRIMARY KEY,
                ""Value"" TEXT NOT NULL,
                ""Description"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ""QrSessions"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_QrSessions"" PRIMARY KEY,
                ""ChallengeNonce"" TEXT NOT NULL,
                ""BrowserSecretHash"" TEXT NOT NULL,
                ""DesktopPublicKey"" TEXT NULL,
                ""MobilePublicKey"" TEXT NULL,
                ""EncryptedApprovalPayload"" TEXT NULL,
                ""ConfirmationCode"" TEXT NOT NULL,
                ""ConfirmationGlyph"" TEXT NOT NULL,
                ""ExchangeTicket"" TEXT NULL,
                ""Status"" INTEGER NOT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""ExpiresAtUtc"" TEXT NOT NULL,
                ""AuthorizedUserId"" TEXT NULL,
                ""ClientIpAddress"" TEXT NULL,
                ""ClientUserAgent"" TEXT NULL,
                ""ApprovedAtUtc"" TEXT NULL,
                ""ConsumedAtUtc"" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_QrSessions_ExpiresAtUtc"" ON ""QrSessions"" (""ExpiresAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_QrSessions_Status"" ON ""QrSessions"" (""Status"");
            CREATE INDEX IF NOT EXISTS ""IX_QrSessions_ExchangeTicket"" ON ""QrSessions"" (""ExchangeTicket"");

            CREATE TABLE IF NOT EXISTS ""AppNotifications"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AppNotifications"" PRIMARY KEY,
                ""RecipientUserId"" TEXT NULL,
                ""RecipientRole"" TEXT NULL,
                ""Title"" TEXT NOT NULL,
                ""Message"" TEXT NOT NULL,
                ""Type"" TEXT NOT NULL,
                ""MetadataJson"" TEXT NULL,
                ""IsRead"" INTEGER NOT NULL,
                ""ReadAtUtc"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""IsDeleted"" INTEGER NOT NULL,
                ""DeletedAtUtc"" TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AppNotifications_RecipientRole_IsRead"" ON ""AppNotifications"" (""RecipientRole"", ""IsRead"");
            CREATE INDEX IF NOT EXISTS ""IX_AppNotifications_RecipientUserId_IsRead"" ON ""AppNotifications"" (""RecipientUserId"", ""IsRead"");
        ");
    }

    private static async Task<bool> TableExistsAsync(DietTrackerDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var checkTableCmd = connection.CreateCommand();
        if (db.Database.IsSqlServer())
        {
            checkTableCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName;";
        }
        else
        {
            checkTableCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @tableName;";
        }
        var p = checkTableCmd.CreateParameter();
        p.ParameterName = "@tableName";
        p.Value = tableName;
        checkTableCmd.Parameters.Add(p);
        var count = Convert.ToInt64(await checkTableCmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task EnsureColumnExistsAsync(DietTrackerDbContext db, string tableName, string columnName, string columnDefinition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var tableExists = await TableExistsAsync(db, tableName);
        if (!tableExists)
        {
            return;
        }

        if (!ValidIdentifierRegex().IsMatch(tableName) || !ValidIdentifierRegex().IsMatch(columnName))
        {
            throw new ArgumentException("Invalid identifier", nameof(tableName));
        }

        var columnExists = false;
        using (var cmd = connection.CreateCommand())
        {
            if (db.Database.IsSqlServer())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";
                var pTable = cmd.CreateParameter();
                pTable.ParameterName = "@tableName";
                pTable.Value = tableName;
                cmd.Parameters.Add(pTable);

                var pCol = cmd.CreateParameter();
                pCol.ParameterName = "@columnName";
                pCol.Value = columnName;
                cmd.Parameters.Add(pCol);

                columnExists = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
            }
            else
            {
#pragma warning disable CA2100 // Table name is validated against strict alphanumeric identifier pattern; SQLite PRAGMA does not support parameterized table names
                cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
#pragma warning restore CA2100
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }
        }

        if (!columnExists)
        {
            using var alterCmd = connection.CreateCommand();
#pragma warning disable CA2100 // Dynamic column addition uses internally sanitized identifiers from trusted schema migrations
            if (db.Database.IsSqlServer())
            {
                var sqlServerDef = columnDefinition
                    .Replace("TEXT", "NVARCHAR(MAX)")
                    .Replace("REAL", "FLOAT")
                    .Replace("INTEGER", "INT");
                alterCmd.CommandText = $"ALTER TABLE [{tableName}] ADD [{columnName}] {sqlServerDef};";
            }
            else
            {
                alterCmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
            }
#pragma warning restore CA2100
            await alterCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureIndexExistsAsync(DietTrackerDbContext db, string tableName, string indexName, string columns)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var cmd = connection.CreateCommand();
#pragma warning disable CA2100 // Dynamic index creation uses internally sanitized identifiers from trusted schema migrations
        if (db.Database.IsSqlServer())
        {
            cmd.CommandText = $@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'{indexName}' AND object_id = OBJECT_ID(N'[{tableName}]'))
                BEGIN
                    CREATE INDEX [{indexName}] ON [{tableName}] ({columns});
                END";
        }
        else
        {
            cmd.CommandText = $@"CREATE INDEX IF NOT EXISTS ""{indexName}"" ON ""{tableName}"" ({columns});";
        }
#pragma warning restore CA2100
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task EnsureSqlServerDatabaseMigratedAsync(DietTrackerDbContext db, ILogger logger)
    {
        var mealsExist = await TableExistsAsync(db, "Meals");
        if (!mealsExist)
        {
            await db.Database.EnsureCreatedAsync();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[INIT] Azure SQL Database pristine schema bootstrapped via EnsureCreatedAsync.");
            }
            return;
        }

        // Incremental Table Reconciler (Ensures all domain tables exist even if created under older schema versions)
        if (!await TableExistsAsync(db, "Corrections"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                    [FrequencyCount] INT NOT NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [DeletedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "Corrections", "IX_Corrections_UserId_OriginalDetectedItem", "[UserId], [OriginalDetectedItem]");
        }

        if (!await TableExistsAsync(db, "ProgressPhotos"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [ProgressPhotos] (
                    [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_ProgressPhotos] PRIMARY KEY,
                    [UserId] NVARCHAR(450) NOT NULL,
                    [CapturedAtUtc] DATETIME2 NOT NULL,
                    [WeightKg] FLOAT NOT NULL,
                    [PhotoType] INT NOT NULL,
                    [PhotoUri] NVARCHAR(MAX) NOT NULL,
                    [IsBaseline] BIT NOT NULL,
                    [Notes] NVARCHAR(MAX) NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [DeletedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "ProgressPhotos", "IX_ProgressPhotos_UserId_CapturedAtUtc", "[UserId], [CapturedAtUtc]");
            await EnsureIndexExistsAsync(db, "ProgressPhotos", "IX_ProgressPhotos_UserId_PhotoType", "[UserId], [PhotoType]");
        }

        if (!await TableExistsAsync(db, "AiFeedbacks"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [DeletedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "AiFeedbacks", "IX_AiFeedbacks_UserId_CreatedAtUtc", "[UserId], [CreatedAtUtc]");
            await EnsureIndexExistsAsync(db, "AiFeedbacks", "IX_AiFeedbacks_Rating", "[Rating]");
        }

        if (!await TableExistsAsync(db, "Users"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                    [LastLoginAtUtc] DATETIME2 NULL,
                    [LastActiveUtc] DATETIME2 NULL,
                    [IsAnonymous] BIT NOT NULL DEFAULT 0,
                    [DisplayName] NVARCHAR(MAX) NULL,
                    [RecoveryLookupId] NVARCHAR(64) NULL,
                    [RecoveryKeyHash] NVARCHAR(256) NULL,
                    [RecoverySalt] NVARCHAR(64) NULL,
                    [RecoveryKeyCreatedAtUtc] DATETIME2 NULL,
                    [IsOnboarded] BIT NOT NULL DEFAULT 0,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [DeletedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "Users", "IX_Users_NormalizedEmail", "[NormalizedEmail]");
            await EnsureIndexExistsAsync(db, "Users", "IX_Users_NormalizedMobileNumber", "[NormalizedMobileNumber]");
            await EnsureIndexExistsAsync(db, "Users", "IX_Users_Role", "[Role]");
            await EnsureIndexExistsAsync(db, "Users", "IX_Users_Tier", "[Tier]");
            await EnsureIndexExistsAsync(db, "Users", "IX_Users_RecoveryLookupId", "[RecoveryLookupId]");
        }

        if (!await TableExistsAsync(db, "VerificationOtps"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                );");
            await EnsureIndexExistsAsync(db, "VerificationOtps", "IX_VerificationOtps_UserId_Target", "[UserId], [Target]");
            await EnsureIndexExistsAsync(db, "VerificationOtps", "IX_VerificationOtps_Target_Channel_IsUsed", "[Target], [Channel], [IsUsed]");
            await EnsureIndexExistsAsync(db, "VerificationOtps", "IX_VerificationOtps_ExpiresAtUtc", "[ExpiresAtUtc]");
        }

        if (!await TableExistsAsync(db, "TierConfigurations"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                );");
            await EnsureIndexExistsAsync(db, "TierConfigurations", "IX_TierConfigurations_Tier", "[Tier]");
        }

        if (!await TableExistsAsync(db, "AiUsageLogs"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
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
                );");
            await EnsureIndexExistsAsync(db, "AiUsageLogs", "IX_AiUsageLogs_UserId_TimestampUtc", "[UserId], [TimestampUtc]");
            await EnsureIndexExistsAsync(db, "AiUsageLogs", "IX_AiUsageLogs_UserId_OperationType", "[UserId], [OperationType]");
        }

        if (!await TableExistsAsync(db, "AppSecrets"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [AppSecrets] (
                    [Key] NVARCHAR(128) NOT NULL CONSTRAINT [PK_AppSecrets] PRIMARY KEY,
                    [Value] NVARCHAR(MAX) NOT NULL,
                    [Description] NVARCHAR(512) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );");
        }

        if (!await TableExistsAsync(db, "QrSessions"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [QrSessions] (
                    [Id] NVARCHAR(64) NOT NULL CONSTRAINT [PK_QrSessions] PRIMARY KEY,
                    [ChallengeNonce] NVARCHAR(128) NOT NULL,
                    [BrowserSecretHash] NVARCHAR(128) NOT NULL,
                    [DesktopPublicKey] NVARCHAR(MAX) NULL,
                    [MobilePublicKey] NVARCHAR(MAX) NULL,
                    [EncryptedApprovalPayload] NVARCHAR(MAX) NULL,
                    [ConfirmationCode] NVARCHAR(16) NOT NULL,
                    [ConfirmationGlyph] NVARCHAR(64) NOT NULL,
                    [ExchangeTicket] NVARCHAR(64) NULL,
                    [Status] INT NOT NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [ExpiresAtUtc] DATETIME2 NOT NULL,
                    [AuthorizedUserId] NVARCHAR(MAX) NULL,
                    [ClientIpAddress] NVARCHAR(MAX) NULL,
                    [ClientUserAgent] NVARCHAR(MAX) NULL,
                    [ApprovedAtUtc] DATETIME2 NULL,
                    [ConsumedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "QrSessions", "IX_QrSessions_ExpiresAtUtc", "[ExpiresAtUtc]");
            await EnsureIndexExistsAsync(db, "QrSessions", "IX_QrSessions_Status", "[Status]");
            await EnsureIndexExistsAsync(db, "QrSessions", "IX_QrSessions_ExchangeTicket", "[ExchangeTicket]");
        }

        if (!await TableExistsAsync(db, "AppNotifications"))
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [AppNotifications] (
                    [Id] NVARCHAR(64) NOT NULL CONSTRAINT [PK_AppNotifications] PRIMARY KEY,
                    [RecipientUserId] NVARCHAR(64) NULL,
                    [RecipientRole] NVARCHAR(64) NULL,
                    [Title] NVARCHAR(256) NOT NULL,
                    [Message] NVARCHAR(MAX) NOT NULL,
                    [Type] NVARCHAR(64) NOT NULL,
                    [MetadataJson] NVARCHAR(MAX) NULL,
                    [IsRead] BIT NOT NULL,
                    [ReadAtUtc] DATETIME2 NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [IsDeleted] BIT NOT NULL,
                    [DeletedAtUtc] DATETIME2 NULL
                );");
            await EnsureIndexExistsAsync(db, "AppNotifications", "IX_AppNotifications_RecipientRole_IsRead", "[RecipientRole], [IsRead]");
            await EnsureIndexExistsAsync(db, "AppNotifications", "IX_AppNotifications_RecipientUserId_IsRead", "[RecipientUserId], [IsRead]");
        }

        // Reconcile all incremental columns on existing tables
        await EnsureColumnExistsAsync(db, "Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRating", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRemarks", "TEXT NULL");
        string clientMutationIdDef = db.Database.IsSqlServer() ? "NVARCHAR(128) NULL" : "TEXT NULL";
        await EnsureColumnExistsAsync(db, "Meals", "ClientMutationId", clientMutationIdDef);
        await EnsureColumnExistsAsync(db, "Meals", "ClientTimestampUtc", "TEXT NULL");
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Meals' AND COLUMN_NAME = 'ClientMutationId' AND DATA_TYPE = 'nvarchar' AND CHARACTER_MAXIMUM_LENGTH = -1)
                BEGIN
                    ALTER TABLE [Meals] ALTER COLUMN [ClientMutationId] NVARCHAR(128) NULL;
                END");
        }
        await EnsureIndexExistsAsync(db, "Meals", "IX_Meals_UserId_ClientMutationId", "[UserId], [ClientMutationId]");

        await EnsureColumnExistsAsync(db, "FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnExistsAsync(db, "FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0");
        await EnsureColumnExistsAsync(db, "Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0");
        await EnsureColumnExistsAsync(db, "Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'");
        string boolColDef = db.Database.IsSqlServer() ? "BIT NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0";
        await EnsureColumnExistsAsync(db, "Profiles", "IsOnboarded", boolColDef);
        await EnsureColumnExistsAsync(db, "Users", "IsOnboarded", boolColDef);
        await EnsureSoftDeleteColumnsAsync(db);

        // Reconcile Users table columns for legacy Azure SQL installations
        await EnsureColumnExistsAsync(db, "Users", "IsAnonymous", "BIT NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Users", "DisplayName", "NVARCHAR(MAX) NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryLookupId", "NVARCHAR(64) NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryKeyHash", "NVARCHAR(256) NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoverySalt", "NVARCHAR(64) NULL");
        await EnsureColumnExistsAsync(db, "Users", "RecoveryKeyCreatedAtUtc", "DATETIME2 NULL");
        await EnsureColumnExistsAsync(db, "Users", "TermsAcceptedAtUtc", "DATETIME2 NULL");
        await EnsureColumnExistsAsync(db, "Users", "TermsVersionAccepted", "NVARCHAR(MAX) NULL");
        await EnsureColumnExistsAsync(db, "Users", "HealthConsentAcceptedAtUtc", "DATETIME2 NULL");
        await EnsureColumnExistsAsync(db, "Users", "HealthConsentVersionAccepted", "NVARCHAR(MAX) NULL");
        await EnsureColumnExistsAsync(db, "Users", "ConsentIpAddress", "NVARCHAR(MAX) NULL");
        await EnsureColumnExistsAsync(db, "Users", "ConsentUserAgent", "NVARCHAR(MAX) NULL");
        await EnsureColumnExistsAsync(db, "Users", "LastLoginAtUtc", "DATETIME2 NULL");
        await EnsureColumnExistsAsync(db, "Users", "LastActiveUtc", "DATETIME2 NULL");
        await EnsureIndexExistsAsync(db, "Users", "IX_Users_LastActiveUtc", "[LastActiveUtc]");
        await EnsureIndexExistsAsync(db, "Users", "IX_Users_RecoveryLookupId", "[RecoveryLookupId]");

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[INIT] Azure SQL Database incremental schema and columns verified successfully.");
        }
    }

    private static async Task EnsureSoftDeleteColumnsAsync(DietTrackerDbContext db)
    {
        string boolDef = db.Database.IsSqlServer() ? "BIT NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0";
        string dateDef = db.Database.IsSqlServer() ? "DATETIME2 NULL" : "TEXT NULL";

        string[] softDeletableTables = ["Meals", "ProgressPhotos", "Users", "Profiles", "Corrections", "Ledgers", "AiFeedbacks"];
        foreach (var table in softDeletableTables)
        {
            await EnsureColumnExistsAsync(db, table, "IsDeleted", boolDef);
            await EnsureColumnExistsAsync(db, table, "DeletedAtUtc", dateDef);
        }
    }

    private static async Task SeedAppSecretsAsync(
        DietTrackerDbContext db,
        IConfiguration configuration,
        Nutrition.Application.Common.Interfaces.IAppEnvironment appEnv,
        ILogger logger)
    {
        var defaultSecrets = new List<(string Key, string FallbackValue, string Description)>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        if (!appEnv.IsDevelopment)
        {
            // GAP-18: Sole Authority - Do not copy Key Vault secrets to database AppSecrets table in production
            return;
        }

        var devJwtKey = !string.IsNullOrWhiteSpace(jwtOptions.Key)
            ? jwtOptions.Key
            : "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";

        defaultSecrets.Add(("Jwt:Key", devJwtKey, "Cryptographic signing key for JWT HMAC-SHA256 tokens (Development Only)"));
        defaultSecrets.Add(("AI:GoogleAI:ApiKey", aiOptions.GoogleAI?.ApiKey ?? string.Empty, "Google Gemini Vision API Key"));
        defaultSecrets.Add(("AI:AzureOpenAI:ApiKey", aiOptions.AzureOpenAI?.ApiKey ?? string.Empty, "Azure OpenAI API Key"));

        if (appEnv.AllowsDemoUsers)
        {
            defaultSecrets.Add(("Auth:DemoPassword", authOptions.DemoPassword, "Deterministic password for seeded demo tier accounts (Debug/Dev only)"));
        }

        foreach (var (key, fallbackValue, description) in defaultSecrets)
        {
            var existing = await db.AppSecrets.FirstOrDefaultAsync(s => s.Key == key);
            if (existing == null)
            {
                await db.AppSecrets.AddAsync(new AppSecret
                {
                    Key = key,
                    Value = fallbackValue,
                    Description = description,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Seeded database secret into AppSecrets table: {Key}", key);
                }
            }
        }

        await db.SaveChangesAsync();
        (configuration as IConfigurationRoot)?.Reload();
    }

    private static async Task DeactivateDemoUsersInReleaseModeAsync(DietTrackerDbContext db, ILogger logger)
    {
        var demoUsers = await db.Users
            .Where(u => ApplicationUser.DemoEmails.Contains(u.Email) ||
                        u.Id.StartsWith("user-free") ||
                        u.Id.StartsWith("user-basic") ||
                        u.Id.StartsWith("user-premium") ||
                        u.Id.StartsWith("user-admin") ||
                        u.Id.StartsWith("user-superadmin"))
            .ToListAsync();

        if (demoUsers.Count > 0)
        {
            foreach (var user in demoUsers)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            await db.SaveChangesAsync();
            logger.LogWarning("[SECURITY] Deactivated {Count} demo user account(s) and revoked security stamps to guarantee zero data breach exposure in Release mode.", demoUsers.Count);
        }
    }

    private static async Task DeactivatePrivilegedDemoUsersAsync(DietTrackerDbContext db, ILogger logger)
    {
        var privilegedDemoUsers = await db.Users
            .Where(u => ApplicationUser.PrivilegedDemoEmails.Contains(u.Email) ||
                        u.Id.StartsWith("user-admin"))
            .ToListAsync();

        if (privilegedDemoUsers.Count > 0)
        {
            foreach (var user in privilegedDemoUsers)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            await db.SaveChangesAsync();
            logger.LogWarning("[SECURITY] Deactivated {Count} privileged demo user account(s) (Admin/SuperAdmin) in released environment.", privilegedDemoUsers.Count);
        }
    }

    private static async Task SeedTierConfigurationsAsync(DietTrackerDbContext db, ILogger logger)
    {
        if (!await db.TierConfigurations.AnyAsync())
        {
            await db.TierConfigurations.AddRangeAsync(TierFeatureConfiguration.GetDefaultConfigurations());
            await db.SaveChangesAsync();
            logger.LogInformation("Default tier configurations seeded successfully.");
        }
    }

    public static async Task SeedDemoUsersAsync(
        DietTrackerDbContext db,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IAppEnvironment appEnv,
        ILogger logger)
    {
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var dietitian = serviceProvider.GetRequiredService<ClinicalDietitianService>();

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var primarySuperAdminEmail = !string.IsNullOrWhiteSpace(authOptions.SuperAdminEmail)
            ? authOptions.SuperAdminEmail.Trim()
            : "superadmin@dietdost.app";

        // Seeded end-user demo tier accounts (Free, Basic, Premium) - permitted in showcase environments
        var demoSpecs = new List<DemoUserSpec>
        {
            new(
                Id: "user-free",
                Email: "free@dietdost.app",
                Name: "Free Tier User (Demo)",
                Mobile: "+919876500001",
                Role: UserRole.User,
                Tier: UserTier.Free,
                Cuisine: "North Indian",
                Conditions: new List<string>(),
                Medications: new List<MedicationEntry>()
            ),
            new(
                Id: "user-basic",
                Email: "basic@dietdost.app",
                Name: "Basic Tier User (Demo)",
                Mobile: "+919876500002",
                Role: UserRole.User,
                Tier: UserTier.Basic,
                Cuisine: "South Indian",
                Conditions: new List<string> { "Hypertension" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Telmisartan 40mg", Dosage = "40mg", Frequency = "Morning" }
                }
            ),
            new(
                Id: "user-premium",
                Email: "premium@dietdost.app",
                Name: "Premium Tier User (Demo)",
                Mobile: "+919876500003",
                Role: UserRole.User,
                Tier: UserTier.Premium,
                Cuisine: "Gujarati",
                Conditions: new List<string> { "Pre-Diabetes" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
                }
            )
        };

        // Privileged Admin & SuperAdmin demo accounts - strictly permitted ONLY in local Debug/Development builds
        if (appEnv.AllowsAdminDemoUsers)
        {
            demoSpecs.Add(new(
                Id: "user-admin",
                Email: "admin.demo@dietdost.app",
                Name: "Admin Tier User (Demo)",
                Mobile: "+919876500004",
                Role: UserRole.Admin,
                Tier: UserTier.Premium,
                Cuisine: "Maharashtrian",
                Conditions: new List<string>(),
                Medications: new List<MedicationEntry>()
            ));

            if (!string.Equals(primarySuperAdminEmail, "superadmin@dietdost.app", StringComparison.OrdinalIgnoreCase))
            {
                demoSpecs.Add(new(
                    Id: "user-superadmin-demo",
                    Email: "superadmin@dietdost.app",
                    Name: "SuperAdmin Tier User (Demo)",
                    Mobile: "+919999999999",
                    Role: UserRole.SuperAdmin,
                    Tier: UserTier.SuperAdmin,
                    Cuisine: "North Indian",
                    Conditions: new List<string> { "Pre-Diabetes" },
                    Medications: new List<MedicationEntry>
                    {
                        new() { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
                    }
                ));
            }
        }
        else
        {
            logger.LogInformation("[SECURITY] Released environment detected: Admin demo user seeding is suppressed. Only Free, Basic, and Premium demo users are seeded.");
            await DeactivatePrivilegedDemoUsersAsync(db, logger);
        }

        var demoPassword = !string.IsNullOrWhiteSpace(authOptions.DemoPassword)
            ? authOptions.DemoPassword
            : (await db.AppSecrets.Where(s => s.Key == "Auth:DemoPassword").Select(s => s.Value).FirstOrDefaultAsync())
            ?? "DietDost@Demo2026!";

        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo("Asia/Kolkata");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));

        foreach (var spec in demoSpecs)
        {
            var normEmail = ApplicationUser.NormalizeEmailAddress(spec.Email);
            var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normEmail);
            if (user == null)
            {
                user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == spec.Id);
                if (user != null)
                {
                    user.Email = spec.Email;
                    user.NormalizedEmail = normEmail;
                    user.PasswordHash = passwordHasher.HashPassword(demoPassword);
                    user.Role = spec.Role;
                    user.Tier = spec.Tier;
                    user.IsEmailVerified = true;
                    user.IsMobileVerified = true;
                    user.IsActive = true;
                    user.IsOnboarded = true;
                    user.IsDeleted = false;
                    user.DeletedAtUtc = null;
                    await db.SaveChangesAsync();
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Updated demo user email to match configuration: {Email} ({Tier}, {Role})", spec.Email, spec.Tier, spec.Role);
                    }
                }
                else
                {
                    user = new ApplicationUser
                    {
                        Id = spec.Id,
                        Email = spec.Email,
                        NormalizedEmail = normEmail,
                        MobileNumber = spec.Mobile,
                        NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber(spec.Mobile),
                        PasswordHash = passwordHasher.HashPassword(demoPassword),
                        SecurityStamp = Guid.NewGuid().ToString("N"),
                        Role = spec.Role,
                        Tier = spec.Tier,
                        IsEmailVerified = true,
                        IsMobileVerified = true,
                        IsActive = true,
                        IsOnboarded = true,
                        TermsAcceptedAtUtc = DateTime.UtcNow,
                        TermsVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.TermsVersion) ? authOptions.TermsVersion : "v1.0-202609",
                        HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                        HealthConsentVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.HealthConsentVersion) ? authOptions.HealthConsentVersion : "v1.0-202609",
                        ConsentIpAddress = "127.0.0.1",
                        ConsentUserAgent = "SystemBootstrap",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    await db.Users.AddAsync(user);
                    await db.SaveChangesAsync();
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Provisioned demo user: {Email} ({Tier}, {Role})", spec.Email, spec.Tier, spec.Role);
                    }
                }
            }
            else
            {
                user.PasswordHash = passwordHasher.HashPassword(demoPassword);
                user.Role = spec.Role;
                user.Tier = spec.Tier;
                user.IsEmailVerified = true;
                user.IsMobileVerified = true;
                user.IsActive = true;
                user.IsOnboarded = true;
                user.IsDeleted = false;
                user.DeletedAtUtc = null;
                await db.SaveChangesAsync();
            }

            var profile = await db.Profiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == user.Id);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    Id = user.Id,
                    Name = spec.Name,
                    Sex = BiologicalSex.Male,
                    Age = 32,
                    HeightCm = 175,
                    CurrentWeightKg = 80,
                    TargetWeightKg = 72,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = spec.Cuisine,
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = spec.Conditions,
                    Medications = spec.Medications,
                    IsOnboarded = true
                };
                await db.Profiles.AddAsync(profile);
                await db.SaveChangesAsync();
            }
            else
            {
                profile.IsDeleted = false;
                profile.DeletedAtUtc = null;
            profile.IsOnboarded = true;
                profile.Name = spec.Name;
                profile.RegionalCuisine = spec.Cuisine;
                profile.IsOnboarded = true;
                await db.SaveChangesAsync();
            }

            await dietitian.GetOrCreateDailyLedgerAsync(user.Id, today);

            // Seed sample progress comparison photos if none exist
            if (!await db.ProgressPhotos.AnyAsync(p => p.UserId == user.Id))
            {
                var baselineDate = DateTime.UtcNow.AddDays(-30);
                var currentDate = DateTime.UtcNow;

                var samplePhotos = new List<ProgressPhoto>
                {
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = baselineDate,
                        WeightKg = 85.0,
                        PhotoType = ProgressPhotoType.Face,
                        PhotoUri = "/uploads/progress/face_baseline.svg",
                        IsBaseline = true,
                        Notes = "Day 1 Baseline photo before commencing ICMR-NIN protocol."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = currentDate,
                        WeightKg = 81.5,
                        PhotoType = ProgressPhotoType.Face,
                        PhotoUri = "/uploads/progress/face_current.svg",
                        IsBaseline = false,
                        Notes = "Day 30 check-in: Noticeable jawline definition and facial slimming."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = baselineDate,
                        WeightKg = 85.0,
                        PhotoType = ProgressPhotoType.FullBodyFront,
                        PhotoUri = "/uploads/progress/body_baseline.svg",
                        IsBaseline = true,
                        Notes = "Day 1 Full Body Front View."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = currentDate,
                        WeightKg = 81.5,
                        PhotoType = ProgressPhotoType.FullBodyFront,
                        PhotoUri = "/uploads/progress/body_current.svg",
                        IsBaseline = false,
                        Notes = "Day 30 Full Body Front View: Down 3.5 kg."
                    }
                };
                await db.ProgressPhotos.AddRangeAsync(samplePhotos);
                await db.SaveChangesAsync();
            }

            // Seed representative Indian meals for rich dashboard view
            if (!await db.Meals.AnyAsync(m => m.UserId == user.Id))
            {
                var now = DateTime.UtcNow;
                var sampleMeals = new List<MealLog>
                {
                    new()
                    {
                        UserId = user.Id,
                        LoggedAt = now.AddHours(-3),
                        MealType = MealType.Lunch,
                        DishName = "North Indian Thali (Phulkas, Dal & Bhindi Masala)",
                        PhotoUri = "/uploads/meals/sample_lunch_thali.jpg",
                        OverallConfidenceScore = 0.96,
                        IsVerifiedByUser = true,
                        DietitianAdvice = "Balanced meal with adequate protein and high-fiber okra. Great portion control under 550 kcal.",
                        Items = new List<FoodItemRecord>
                        {
                            new() { Name = "Whole Wheat Roti", HindiOrRegionalName = "Phulka / Roti", EstimatedPortion = "2 Phulkas", Quantity = 2, Grams = 60, Calories = 70, ProteinGrams = 2.2, CarbsGrams = 15.0, FatGrams = 0.4, FiberGrams = 2.2, SugarGrams = 0.25, SodiumMg = 5 },
                            new() { Name = "Yellow Moong Dal Tadka", HindiOrRegionalName = "Moong Dal Fry", EstimatedPortion = "1 Katori", Quantity = 1, Grams = 150, Calories = 135, ProteinGrams = 7.2, CarbsGrams = 19.0, FatGrams = 4.5, FiberGrams = 5.2, SugarGrams = 0.8, SodiumMg = 320 },
                            new() { Name = "Bhindi Masala (Okra Subzi)", HindiOrRegionalName = "Bhindi ki Sabzi", EstimatedPortion = "1 Katori", Quantity = 1, Grams = 120, Calories = 115, ProteinGrams = 2.8, CarbsGrams = 9.5, FatGrams = 7.2, FiberGrams = 4.8, SugarGrams = 1.5, SodiumMg = 180 },
                            new() { Name = "Green Salad (Cucumber & Tomato)", HindiOrRegionalName = "Kachumber Salad", EstimatedPortion = "1 Plate", Quantity = 1, Grams = 100, Calories = 30, ProteinGrams = 1.0, CarbsGrams = 6.0, FatGrams = 0.2, FiberGrams = 0.8, SugarGrams = 5.9, SodiumMg = 20 }
                        }
                    },
                    new()
                    {
                        UserId = user.Id,
                        LoggedAt = now.AddHours(-7),
                        MealType = MealType.Breakfast,
                        DishName = "Kanda Poha with Roasted Peanuts",
                        OverallConfidenceScore = 0.92,
                        IsVerifiedByUser = true,
                        DietitianAdvice = "Wholesome complex carbs. Peanuts provide good monounsaturated fats.",
                        Items = new List<FoodItemRecord>
                        {
                            new() { Name = "Kanda Poha", HindiOrRegionalName = "Poha", EstimatedPortion = "1 Plate (150g)", Quantity = 1, Grams = 150, Calories = 220, ProteinGrams = 4.5, CarbsGrams = 38.0, FatGrams = 5.5, FiberGrams = 3.2, SugarGrams = 1.8, SodiumMg = 260 },
                            new() { Name = "Roasted Peanuts", HindiOrRegionalName = "Moongphali", EstimatedPortion = "1 Tbsp (15g)", Quantity = 1, Grams = 15, Calories = 85, ProteinGrams = 3.8, CarbsGrams = 2.4, FatGrams = 7.2, FiberGrams = 1.2, SugarGrams = 0.6, SodiumMg = 10 },
                            new() { Name = "Masala Chai (Low Sugar)", HindiOrRegionalName = "Chai", EstimatedPortion = "1 Cup (120ml)", Quantity = 1, Grams = 120, Calories = 65, ProteinGrams = 2.2, CarbsGrams = 8.5, FatGrams = 2.4, FiberGrams = 0.0, SugarGrams = 5.0, SodiumMg = 40 }
                        }
                    },
                    new()
                    {
                        UserId = user.Id,
                        LoggedAt = now.AddDays(-1).Date.AddHours(20),
                        MealType = MealType.Dinner,
                        DishName = "Palak Paneer with Phulkas",
                        OverallConfidenceScore = 0.95,
                        IsVerifiedByUser = true,
                        DietitianAdvice = "High biological value protein from paneer. Low carb dinner supports optimal insulin sensitivity.",
                        Items = new List<FoodItemRecord>
                        {
                            new() { Name = "Palak Paneer", HindiOrRegionalName = "Palak Paneer", EstimatedPortion = "1 Katori (150g)", Quantity = 1, Grams = 150, Calories = 220, ProteinGrams = 12.0, CarbsGrams = 8.5, FatGrams = 15.5, FiberGrams = 4.5, SugarGrams = 2.4, SodiumMg = 380 },
                            new() { Name = "Whole Wheat Roti", HindiOrRegionalName = "Phulka", EstimatedPortion = "2 Phulkas", Quantity = 2, Grams = 60, Calories = 70, ProteinGrams = 2.2, CarbsGrams = 15.0, FatGrams = 0.4, FiberGrams = 2.2, SugarGrams = 0.25, SodiumMg = 5 }
                        }
                    },
                    new()
                    {
                        UserId = user.Id,
                        LoggedAt = now.AddDays(-2).Date.AddHours(20).AddMinutes(15),
                        MealType = MealType.Dinner,
                        DishName = "Moong Dal Khichdi with Curd",
                        OverallConfidenceScore = 0.97,
                        IsVerifiedByUser = true,
                        DietitianAdvice = "Gentle on digestion and gut-friendly with probiotic curd.",
                        Items = new List<FoodItemRecord>
                        {
                            new() { Name = "Moong Dal Khichdi", HindiOrRegionalName = "Khichdi", EstimatedPortion = "1.5 Bowl (250g)", Quantity = 1, Grams = 250, Calories = 270, ProteinGrams = 9.8, CarbsGrams = 45.0, FatGrams = 5.5, FiberGrams = 5.0, SugarGrams = 1.2, SodiumMg = 340 },
                            new() { Name = "Plain Cow Milk Curd / Dahi", HindiOrRegionalName = "Dahi", EstimatedPortion = "1 Katori (100g)", Quantity = 1, Grams = 100, Calories = 60, ProteinGrams = 3.5, CarbsGrams = 4.5, FatGrams = 3.2, FiberGrams = 0.0, SugarGrams = 4.2, SodiumMg = 38 }
                        }
                    }
                };

                foreach (var meal in sampleMeals)
                {
                    meal.RecalculateTotals();
                }

                db.Meals.AddRange(sampleMeals);
                await db.SaveChangesAsync();
            }
        }

        // Seed demo anonymous zero-PII user (usr_87c369c1e3f8 / "Nikunj QR") with user's verified recovery kit
        var demoMnemonic = "suspect vintage animal morning require leader genre youth very home trust jump";
        Bip39Mnemonic.ValidateMnemonic(demoMnemonic, out var masterEntropy);
        var derivedLookupId = RecoveryKit.DeriveLookupId(masterEntropy);
        var derivedSaltBytes = Convert.FromHexString(string.Concat("a1b2c3d4", "e5f67890", "12345678", "9abcdef0"));
        var derivedHashHex = RecoveryKit.ComputeKeyHash(masterEntropy, derivedSaltBytes);
        var derivedSaltHex = Convert.ToHexString(derivedSaltBytes).ToLowerInvariant();

        var existingAnon = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8" || u.RecoveryLookupId == derivedLookupId);
        if (existingAnon == null)
        {
            var anonUser = new ApplicationUser
            {
                Id = "usr_87c369c1e3f8",
                Email = "usr_87c369c1e3f8@dietdost.local",
                NormalizedEmail = "USR_87C369C1E3F8@DIETDOST.LOCAL",
                DisplayName = "Nikunj QR",
                MobileNumber = "ANONYMOUS",
                NormalizedMobileNumber = "ANONYMOUS",
                IsAnonymous = true,
                Role = UserRole.User,
                Tier = UserTier.Free,
                IsEmailVerified = true,
                IsMobileVerified = false,
                PasswordHash = "ANONYMOUS_NO_PASSWORD",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = "v1.0-202609",
                ConsentIpAddress = "127.0.0.1",
                ConsentUserAgent = "DietDost-Seed/1.0",
                RecoveryLookupId = derivedLookupId,
                RecoverySalt = derivedSaltHex,
                RecoveryKeyHash = derivedHashHex,
                RecoveryKeyCreatedAtUtc = DateTime.UtcNow,
                IsOnboarded = true
            };
            db.Users.Add(anonUser);

            var anonProfile = await db.Profiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == anonUser.Id);
            if (anonProfile == null)
            {
                anonProfile = new UserProfile
                {
                    Id = anonUser.Id,
                    Name = "Nikunj QR",
                    Sex = BiologicalSex.Male,
                    Age = 35,
                    HeightCm = 175,
                    CurrentWeightKg = 78.0,
                    TargetWeightKg = 72.0,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = "Gujarati",
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = new List<string>(),
                    Medications = new List<MedicationEntry>(),
                    IsOnboarded = true
                };
                db.Profiles.Add(anonProfile);
            }
            else
            {
                anonProfile.IsDeleted = false;
                anonProfile.DeletedAtUtc = null;
                anonProfile.Name = "Nikunj QR";
                anonProfile.IsOnboarded = true;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("[SEED] Seeded demo zero-PII user 'usr_87c369c1e3f8' ('Nikunj QR') with permanent recovery kit.");
        }
        else
        {
            if (existingAnon.IsDeleted)
            {
                existingAnon.IsDeleted = false;
                existingAnon.DeletedAtUtc = null;
                existingAnon.IsActive = true;
                existingAnon.IsOnboarded = true;
            }

            // Reconcile credentials to guarantee the demo recovery mnemonic always matches
            if (existingAnon.RecoveryLookupId != derivedLookupId ||
                existingAnon.RecoveryKeyHash != derivedHashHex ||
                existingAnon.RecoverySalt != derivedSaltHex)
            {
                existingAnon.RecoveryLookupId = derivedLookupId;
                existingAnon.RecoveryKeyHash = derivedHashHex;
                existingAnon.RecoverySalt = derivedSaltHex;
                existingAnon.RecoveryKeyCreatedAtUtc = DateTime.UtcNow;
            }

            var anonProfile = await db.Profiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == existingAnon.Id);
            if (anonProfile == null)
            {
                anonProfile = new UserProfile
                {
                    Id = existingAnon.Id,
                    Name = "Nikunj QR",
                    Sex = BiologicalSex.Male,
                    Age = 35,
                    HeightCm = 175,
                    CurrentWeightKg = 78.0,
                    TargetWeightKg = 72.0,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = "Gujarati",
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = new List<string>(),
                    Medications = new List<MedicationEntry>(),
                    IsOnboarded = true
                };
                db.Profiles.Add(anonProfile);
            }
            else
            {
                if (anonProfile.IsDeleted)
                {
                    anonProfile.IsDeleted = false;
                    anonProfile.DeletedAtUtc = null;
                }
                anonProfile.Name = "Nikunj QR";
                anonProfile.IsOnboarded = true;
            }

            await db.SaveChangesAsync();
            logger.LogInformation("[SEED] Reconciled demo zero-PII user 'usr_87c369c1e3f8' ('Nikunj QR') credentials with permanent mnemonic.");
        }
    }

    private static async Task MigrateLegacyDataAsync(DietTrackerDbContext db, ILogger logger)
    {
        var activeSuperAdmin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin);
        if (activeSuperAdmin != null)
        {
            var updateSql = db.Database.IsSqlServer()
                ? @"
                    UPDATE [Profiles] SET [Id] = {0} WHERE [Id] = 'user-default';
                    UPDATE [Meals] SET [UserId] = {0} WHERE [UserId] = 'user-default';
                    UPDATE [Ledgers] SET [UserId] = {0} WHERE [UserId] = 'user-default';
                    UPDATE [ProgressPhotos] SET [UserId] = {0} WHERE [UserId] = 'user-default';
                    UPDATE [Corrections] SET [UserId] = {0} WHERE [UserId] = 'user-default';
                    UPDATE [AiFeedbacks] SET [UserId] = {0} WHERE [UserId] = 'user-default';"
                : @"
                    UPDATE ""Profiles"" SET ""Id"" = {0} WHERE ""Id"" = 'user-default';
                    UPDATE ""Meals"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                    UPDATE ""Ledgers"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                    UPDATE ""ProgressPhotos"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                    UPDATE ""Corrections"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                    UPDATE ""AiFeedbacks"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';";

            await db.Database.ExecuteSqlRawAsync(updateSql, activeSuperAdmin.Id);
        }
    }

    public static async Task EnsureSuperAdminAccountAsync(
        DietTrackerDbContext db,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger logger)
    {
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var dietitian = serviceProvider.GetRequiredService<ClinicalDietitianService>();

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var primarySuperAdminEmail = !string.IsNullOrWhiteSpace(authOptions.SuperAdminEmail)
            ? authOptions.SuperAdminEmail.Trim()
            : (!string.IsNullOrWhiteSpace(configuration["SUPER_ADMIN_EMAIL"])
                ? configuration["SUPER_ADMIN_EMAIL"]!.Trim()
                : "nikunj_banker@hotmail.com");

        var superAdminPassword = !string.IsNullOrWhiteSpace(authOptions.SuperAdminPassword)
            ? authOptions.SuperAdminPassword
            : (!string.IsNullOrWhiteSpace(configuration["SUPER_ADMIN_PASSWORD"])
                ? configuration["SUPER_ADMIN_PASSWORD"]!
                : (!string.IsNullOrWhiteSpace(authOptions.DemoPassword)
                    ? authOptions.DemoPassword
                    : "DietDost@Demo2026!"));

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(primarySuperAdminEmail);
        var superAdmin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (superAdmin == null)
        {
            superAdmin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == "superadmin-root");
        }

        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                Id = "superadmin-root",
                Email = primarySuperAdminEmail,
                NormalizedEmail = normalizedEmail,
                MobileNumber = "+919999999990",
                NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+919999999990"),
                PasswordHash = passwordHasher.HashPassword(superAdminPassword),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Role = UserRole.SuperAdmin,
                Tier = UserTier.SuperAdmin,
                IsEmailVerified = true,
                IsMobileVerified = true,
                IsActive = true,
                IsOnboarded = true,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.TermsVersion) ? authOptions.TermsVersion : "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.HealthConsentVersion) ? authOptions.HealthConsentVersion : "v1.0-202609",
                ConsentIpAddress = "127.0.0.1",
                ConsentUserAgent = "SystemBootstrap",
                CreatedAtUtc = DateTime.UtcNow
            };
            await db.Users.AddAsync(superAdmin);
            await db.SaveChangesAsync();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[SECURITY] Autonomous bootstrap completed: Created root SuperAdmin account for {Email} ({Tier}, {Role}).", superAdmin.Email, superAdmin.Tier, superAdmin.Role);
            }
        }
        else
        {
            superAdmin.Email = primarySuperAdminEmail;
            superAdmin.NormalizedEmail = normalizedEmail;
            superAdmin.Role = UserRole.SuperAdmin;
            superAdmin.Tier = UserTier.SuperAdmin;
            superAdmin.IsEmailVerified = true;
            superAdmin.IsMobileVerified = true;
            superAdmin.MobileNumber = "+919999999990";
            superAdmin.NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+919999999990");
            superAdmin.IsActive = true;
            superAdmin.IsOnboarded = true;
            superAdmin.IsDeleted = false;
            superAdmin.DeletedAtUtc = null;
            if (string.IsNullOrWhiteSpace(superAdmin.PasswordHash))
            {
                superAdmin.PasswordHash = passwordHasher.HashPassword(superAdminPassword);
            }
            await db.SaveChangesAsync();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[SECURITY] Autonomous synchronization completed: Reconciled root SuperAdmin account for {Email} ({Tier}, {Role}).", superAdmin.Email, superAdmin.Tier, superAdmin.Role);
            }
        }

        var profile = await db.Profiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == superAdmin.Id);
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = superAdmin.Id,
                Name = "Nikunj Banker (SuperAdmin)",
                Sex = BiologicalSex.Male,
                Age = 35,
                HeightCm = 175,
                CurrentWeightKg = 75,
                TargetWeightKg = 70,
                DesiredPaceKgPerWeek = 0.5,
                ActivityLevel = ActivityLevel.Moderate,
                DietaryPreference = DietaryPreference.LactoVeg,
                RegionalCuisine = "North Indian",
                Timezone = "Asia/Kolkata",
                IsOnboarded = true
            };
            await db.Profiles.AddAsync(profile);
            await db.SaveChangesAsync();
        }
        else
        {
            profile.IsDeleted = false;
            profile.DeletedAtUtc = null;
            profile.IsOnboarded = true;
            await db.SaveChangesAsync();
        }

        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo("Asia/Kolkata");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));
        await dietitian.GetOrCreateDailyLedgerAsync(superAdmin.Id, today);
    }

    private sealed record DemoUserSpec(
        string Id,
        string Email,
        string Name,
        string Mobile,
        UserRole Role,
        UserTier Tier,
        string Cuisine,
        List<string> Conditions,
        List<MedicationEntry> Medications);

    private static readonly string[] SeedMediaSubdirectories = ["meals", "progress"];

    /// <summary>
    /// Mirrors seed media assets (sample meals and baseline progress photos) from the container's
    /// wwwroot/uploads directory to persistent storage (e.g. Azure Files SMB share at /app/data/wwwroot/uploads)
    /// if they do not exist yet. Prevents HTTP 404 for seed media in production environments.
    /// </summary>
    private static void EnsureSeedMediaAssetsMirrored(IServiceProvider services, ILogger logger)
    {
        try
        {
            var hostEnv = services.GetService<IWebHostEnvironment>();
            var storageOptions = services.GetService<Microsoft.Extensions.Options.IOptions<Nutrition.Application.Common.Options.StorageOptions>>()?.Value;

            if (hostEnv == null || string.IsNullOrWhiteSpace(storageOptions?.WebRootPath))
            {
                return;
            }

            var sourceUploads = Path.Combine(hostEnv.WebRootPath, "uploads");
            var destUploads = Path.Combine(storageOptions.WebRootPath, "uploads");

            if (!Directory.Exists(sourceUploads))
            {
                return;
            }

            if (string.Equals(Path.GetFullPath(sourceUploads), Path.GetFullPath(destUploads), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var subDir in SeedMediaSubdirectories)
            {
                var srcDir = Path.Combine(sourceUploads, subDir);
                var dstDir = Path.Combine(destUploads, subDir);

                if (!Directory.Exists(srcDir)) continue;
                Directory.CreateDirectory(dstDir);

                foreach (var file in Directory.GetFiles(srcDir))
                {
                    var destFile = Path.Combine(dstDir, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile, overwrite: false);
                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            var fileName = Path.GetFileName(file);
                            logger.LogInformation("[STORAGE] Mirrored seed media asset '{FileName}' to persistent storage volume '{DestPath}'.", fileName, destFile);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[STORAGE] Non-fatal warning while mirroring seed media assets to persistent volume.");
        }
    }
}



/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Initializers;

/// <summary>
/// Strategy implementation for SQLite database initialization, PRAGMA configuration,
/// SMB lock resilience, and incremental schema migrations.
/// </summary>
public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private static readonly string[] ScriptSeparators = [";\r\n", ";\n"];
    private readonly DietTrackerDbContext _db;
    private readonly ILogger<SqliteDatabaseInitializer> _logger;

    public string ProviderName => "Sqlite";

    public SqliteDatabaseInitializer(DietTrackerDbContext db, ILogger<SqliteDatabaseInitializer> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
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
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        var attempt = args.AttemptNumber + 1;
                        var delaySeconds = (int)args.RetryDelay.TotalSeconds;
                        _logger.LogWarning(args.Outcome.Exception, "SQLite database is temporarily locked (attempt {Attempt}/5). Waiting {DelaySeconds}s for lock release on SMB share...", attempt, delaySeconds);
                    }
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        await sqliteRetryPipeline.ExecuteAsync(async ct => await EnsureSchemaAsync(ct), cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();

        // Fast Pristine Schema Bootstrapping:
        // Azure Files SMB / CIFS mounts have high per-operation latency (~800ms) and do not support
        // POSIX byte-range locking required by SQLite transactions. If target file is missing or 0 bytes,
        // bootstrap pristine schema in container /tmp and copy to target volume in single atomic I/O.
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
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("[INIT] Pristine SQLite bootstrapping initiated for '{DbPath}' via local container temporary storage...", fullPath);
                    }
                    var tempFile = Path.Combine(Path.GetTempPath(), $"diet_dost_init_{Guid.NewGuid():N}.db");
                    try
                    {
                        var tempOptions = new DbContextOptionsBuilder<DietTrackerDbContext>()
                            .UseSqlite($"Data Source={tempFile}")
                            .Options;

                        using (var tempDb = new DietTrackerDbContext(tempOptions))
                        {
                            await tempDb.Database.EnsureCreatedAsync(cancellationToken);
                            await tempDb.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode = 'DELETE';", cancellationToken);
                        }

                        var targetDir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(tempFile, fullPath, overwrite: true);
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("[INIT] Pristine SQLite schema successfully deployed to '{DbPath}'.", fullPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[INIT] Local pristine schema bootstrap encountered an exception; falling back to in-place creation.");
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
            await connection.OpenAsync(cancellationToken);
        }

        var mealsExist = await TableExistsAsync("Meals", cancellationToken);
        if (!mealsExist)
        {
            var createScript = _db.Database.GenerateCreateScript();
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
                    await createCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch
                {
                    // Table, index, or constraint may already exist
                }
            }
            return;
        }

        await _db.Database.ExecuteSqlRawAsync(@"
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
            );", cancellationToken);

        await EnsureColumnExistsAsync("Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRating", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRemarks", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "ClientMutationId", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "ClientTimestampUtc", "TEXT NULL", cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Meals_UserId_ClientMutationId"" ON ""Meals"" (""UserId"", ""ClientMutationId"");", cancellationToken);
        await EnsureColumnExistsAsync("FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnExistsAsync("FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "HealthScoreStatusText", "TEXT NOT NULL DEFAULT 'Awaiting First Meal 🌅'", cancellationToken);
        await EnsureColumnExistsAsync("Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'", cancellationToken);
        await EnsureColumnExistsAsync("Profiles", "IsOnboarded", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Users", "IsOnboarded", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSoftDeleteColumnsAsync(cancellationToken);

        // Users table columns
        await EnsureColumnExistsAsync("Users", "IsAnonymous", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Users", "DisplayName", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryLookupId", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryKeyHash", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoverySalt", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryKeyCreatedAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryLookupId", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyHash", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoverySalt", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyCreatedAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyExpiresAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryIssueTokenHash", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastActivatedRecoveryIssueTokenHash", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "TermsAcceptedAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "TermsVersionAccepted", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "HealthConsentAcceptedAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "HealthConsentVersionAccepted", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "ConsentIpAddress", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "ConsentUserAgent", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastLoginAtUtc", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastActiveUtc", "TEXT NULL", cancellationToken);
        await EnsureIndexExistsAsync("Users", "IX_Users_LastActiveUtc", "\"LastActiveUtc\"", cancellationToken);
        await EnsureIndexExistsAsync("Users", "IX_Users_RecoveryLookupId", "\"RecoveryLookupId\"", cancellationToken);

        await _db.Database.ExecuteSqlRawAsync(@"
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
                ""PendingRecoveryLookupId"" TEXT NULL,
                ""PendingRecoveryKeyHash"" TEXT NULL,
                ""PendingRecoverySalt"" TEXT NULL,
                ""PendingRecoveryKeyCreatedAtUtc"" TEXT NULL,
                ""PendingRecoveryKeyExpiresAtUtc"" TEXT NULL,
                ""PendingRecoveryIssueTokenHash"" TEXT NULL,
                ""LastActivatedRecoveryIssueTokenHash"" TEXT NULL,
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
        ", cancellationToken);
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var checkTableCmd = connection.CreateCommand();
        checkTableCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @tableName;";
        var p = checkTableCmd.CreateParameter();
        p.ParameterName = "@tableName";
        p.Value = tableName;
        checkTableCmd.Parameters.Add(p);
        var count = Convert.ToInt64(await checkTableCmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private async Task EnsureColumnExistsAsync(string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(tableName, cancellationToken);
        if (!tableExists) return;

        var connection = _db.Database.GetDbConnection();
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);

        bool columnFound = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            var colName = reader.GetString(1);
            if (colName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                columnFound = true;
                break;
            }
        }
        await reader.CloseAsync();

        if (!columnFound)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureIndexExistsAsync(string tableName, string indexName, string columns, CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(tableName, cancellationToken);
        if (!tableExists) return;

        var connection = _db.Database.GetDbConnection();
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name = @indexName;";
        var p = checkCmd.CreateParameter();
        p.ParameterName = "@indexName";
        p.Value = indexName;
        checkCmd.Parameters.Add(p);
        var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));

        if (count == 0)
        {
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{tableName}\" ({columns});";
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureSoftDeleteColumnsAsync(CancellationToken cancellationToken)
    {
        string[] softDeletableTables = ["Meals", "ProgressPhotos", "Users", "Profiles", "Corrections", "Ledgers", "AiFeedbacks"];
        foreach (var table in softDeletableTables)
        {
            await EnsureColumnExistsAsync(table, "IsDeleted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnExistsAsync(table, "DeletedAtUtc", "TEXT NULL", cancellationToken);
        }
    }
}

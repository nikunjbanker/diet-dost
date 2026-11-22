/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Initializers;

/// <summary>
/// Strategy implementation for Azure SQL Database initialization, schema migration,
/// and transient error / free tier quota pause resilience.
/// </summary>
public sealed class AzureSqlDatabaseInitializer : IDatabaseInitializer
{
    private readonly DietTrackerDbContext _db;
    private readonly ILogger<AzureSqlDatabaseInitializer> _logger;

    public string ProviderName => "AzureSql";

    public AzureSqlDatabaseInitializer(DietTrackerDbContext db, ILogger<AzureSqlDatabaseInitializer> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        var sqlServerRetryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(ex =>
                    {
                        if (ex.Number == 42119)
                        {
                            _logger.LogCritical(ex, "[AZURE SQL QUOTA EXHAUSTED] Azure SQL Database free tier allowance (100,000 vCore-seconds) is exhausted (Error 42119). Azure auto-paused this database. Run 'az sql db update --free-limit-exhaustion-behavior BillOverUsage' or set 'Database__Provider=Sqlite'.");
                            return false;
                        }
                        return ex.Number == 40613 ||
                               ex.Number == 40197 ||
                               ex.Number == 40501 ||
                               ex.Number == 49918 ||
                               ex.Number == 49919 ||
                               ex.Number == 49920 ||
                               ex.Number == 10928 ||
                               ex.Number == 10929;
                    }),
                MaxRetryAttempts = 6,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(5),
                MaxDelay = TimeSpan.FromSeconds(60),
                OnRetry = args =>
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        var attempt = args.AttemptNumber + 1;
                        var delaySeconds = (int)args.RetryDelay.TotalSeconds;
                        _logger.LogWarning(args.Outcome.Exception, "[WARMUP] Azure SQL Database is auto-resuming from pause (attempt {Attempt}/6). Waiting {DelaySeconds}s for database to become online...", attempt, delaySeconds);
                    }
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        await sqlServerRetryPipeline.ExecuteAsync(async ct => await EnsureSqlServerDatabaseMigratedAsync(ct), cancellationToken);
    }

    public async Task EnsureSqlServerDatabaseMigratedAsync(CancellationToken cancellationToken)
    {
        var mealsExist = await TableExistsAsync("Meals", cancellationToken);
        if (!mealsExist)
        {
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[INIT] Azure SQL Database pristine schema bootstrapped via EnsureCreatedAsync.");
            }
            return;
        }

        // Incremental Table Reconciler
        if (!await TableExistsAsync("Corrections", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("Corrections", "IX_Corrections_UserId_OriginalDetectedItem", "[UserId], [OriginalDetectedItem]", cancellationToken);
        }

        if (!await TableExistsAsync("ProgressPhotos", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("ProgressPhotos", "IX_ProgressPhotos_UserId_CapturedAtUtc", "[UserId], [CapturedAtUtc]", cancellationToken);
            await EnsureIndexExistsAsync("ProgressPhotos", "IX_ProgressPhotos_UserId_PhotoType", "[UserId], [PhotoType]", cancellationToken);
        }

        if (!await TableExistsAsync("AiFeedbacks", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("AiFeedbacks", "IX_AiFeedbacks_UserId_CreatedAtUtc", "[UserId], [CreatedAtUtc]", cancellationToken);
            await EnsureIndexExistsAsync("AiFeedbacks", "IX_AiFeedbacks_Rating", "[Rating]", cancellationToken);
        }

        if (!await TableExistsAsync("Users", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                    [PendingRecoveryLookupId] NVARCHAR(64) NULL,
                    [PendingRecoveryKeyHash] NVARCHAR(256) NULL,
                    [PendingRecoverySalt] NVARCHAR(64) NULL,
                    [PendingRecoveryKeyCreatedAtUtc] DATETIME2 NULL,
                    [PendingRecoveryKeyExpiresAtUtc] DATETIME2 NULL,
                    [PendingRecoveryIssueTokenHash] NVARCHAR(64) NULL,
                    [LastActivatedRecoveryIssueTokenHash] NVARCHAR(64) NULL,
                    [IsOnboarded] BIT NOT NULL DEFAULT 0,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [DeletedAtUtc] DATETIME2 NULL
                );", cancellationToken);
            await EnsureIndexExistsAsync("Users", "IX_Users_NormalizedEmail", "[NormalizedEmail]", cancellationToken);
            await EnsureIndexExistsAsync("Users", "IX_Users_NormalizedMobileNumber", "[NormalizedMobileNumber]", cancellationToken);
            await EnsureIndexExistsAsync("Users", "IX_Users_Role", "[Role]", cancellationToken);
            await EnsureIndexExistsAsync("Users", "IX_Users_Tier", "[Tier]", cancellationToken);
            await EnsureIndexExistsAsync("Users", "IX_Users_RecoveryLookupId", "[RecoveryLookupId]", cancellationToken);
        }

        if (!await TableExistsAsync("VerificationOtps", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("VerificationOtps", "IX_VerificationOtps_UserId_Target", "[UserId], [Target]", cancellationToken);
            await EnsureIndexExistsAsync("VerificationOtps", "IX_VerificationOtps_Target_Channel_IsUsed", "[Target], [Channel], [IsUsed]", cancellationToken);
            await EnsureIndexExistsAsync("VerificationOtps", "IX_VerificationOtps_ExpiresAtUtc", "[ExpiresAtUtc]", cancellationToken);
        }

        if (!await TableExistsAsync("TierConfigurations", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("TierConfigurations", "IX_TierConfigurations_Tier", "[Tier]", cancellationToken);
        }

        if (!await TableExistsAsync("AiUsageLogs", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("AiUsageLogs", "IX_AiUsageLogs_UserId_TimestampUtc", "[UserId], [TimestampUtc]", cancellationToken);
            await EnsureIndexExistsAsync("AiUsageLogs", "IX_AiUsageLogs_UserId_OperationType", "[UserId], [OperationType]", cancellationToken);
        }

        if (!await TableExistsAsync("AppSecrets", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [AppSecrets] (
                    [Key] NVARCHAR(128) NOT NULL CONSTRAINT [PK_AppSecrets] PRIMARY KEY,
                    [Value] NVARCHAR(MAX) NOT NULL,
                    [Description] NVARCHAR(512) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );", cancellationToken);
        }

        if (!await TableExistsAsync("QrSessions", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("QrSessions", "IX_QrSessions_ExpiresAtUtc", "[ExpiresAtUtc]", cancellationToken);
            await EnsureIndexExistsAsync("QrSessions", "IX_QrSessions_Status", "[Status]", cancellationToken);
            await EnsureIndexExistsAsync("QrSessions", "IX_QrSessions_ExchangeTicket", "[ExchangeTicket]", cancellationToken);
        }

        if (!await TableExistsAsync("AppNotifications", cancellationToken))
        {
            await _db.Database.ExecuteSqlRawAsync(@"
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
                );", cancellationToken);
            await EnsureIndexExistsAsync("AppNotifications", "IX_AppNotifications_RecipientRole_IsRead", "[RecipientRole], [IsRead]", cancellationToken);
            await EnsureIndexExistsAsync("AppNotifications", "IX_AppNotifications_RecipientUserId_IsRead", "[RecipientUserId], [IsRead]", cancellationToken);
        }

        // Reconcile columns on existing tables
        await EnsureColumnExistsAsync("Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRating", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRemarks", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "ClientMutationId", "NVARCHAR(128) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Meals", "ClientTimestampUtc", "TEXT NULL", cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Meals' AND COLUMN_NAME = 'ClientMutationId' AND DATA_TYPE = 'nvarchar' AND CHARACTER_MAXIMUM_LENGTH = -1)
            BEGIN
                ALTER TABLE [Meals] ALTER COLUMN [ClientMutationId] NVARCHAR(128) NULL;
            END", cancellationToken);
        await EnsureIndexExistsAsync("Meals", "IX_Meals_UserId_ClientMutationId", "[UserId], [ClientMutationId]", cancellationToken);

        await EnsureColumnExistsAsync("FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnExistsAsync("FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0", cancellationToken);
        await EnsureColumnExistsAsync("Ledgers", "HealthScoreStatusText", "TEXT NOT NULL DEFAULT 'Awaiting First Meal 🌅'", cancellationToken);
        await EnsureColumnExistsAsync("Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'", cancellationToken);
        await EnsureColumnExistsAsync("Profiles", "IsOnboarded", "BIT NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Users", "IsOnboarded", "BIT NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSoftDeleteColumnsAsync(cancellationToken);

        // Users table columns
        await EnsureColumnExistsAsync("Users", "IsAnonymous", "BIT NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnExistsAsync("Users", "DisplayName", "NVARCHAR(MAX) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryLookupId", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryKeyHash", "NVARCHAR(256) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoverySalt", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "RecoveryKeyCreatedAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryLookupId", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyHash", "NVARCHAR(256) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoverySalt", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyCreatedAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryKeyExpiresAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "PendingRecoveryIssueTokenHash", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastActivatedRecoveryIssueTokenHash", "NVARCHAR(64) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "TermsAcceptedAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "TermsVersionAccepted", "NVARCHAR(MAX) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "HealthConsentAcceptedAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "HealthConsentVersionAccepted", "NVARCHAR(MAX) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "ConsentIpAddress", "NVARCHAR(MAX) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "ConsentUserAgent", "NVARCHAR(MAX) NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastLoginAtUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureColumnExistsAsync("Users", "LastActiveUtc", "DATETIME2 NULL", cancellationToken);
        await EnsureIndexExistsAsync("Users", "IX_Users_LastActiveUtc", "[LastActiveUtc]", cancellationToken);
        await EnsureIndexExistsAsync("Users", "IX_Users_RecoveryLookupId", "[RecoveryLookupId]", cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[INIT] Azure SQL Database incremental schema and columns verified successfully.");
        }
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var checkTableCmd = connection.CreateCommand();
        checkTableCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName;";
        var p = checkTableCmd.CreateParameter();
        p.ParameterName = "@tableName";
        p.Value = tableName;
        checkTableCmd.Parameters.Add(p);
        var count = Convert.ToInt64(await checkTableCmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Internal schema maintenance uses compile-time sanitized identifiers")]
    private async Task EnsureColumnExistsAsync(string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(tableName, cancellationToken);
        if (!tableExists) return;

        var connection = _db.Database.GetDbConnection();
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName;";
        var p1 = checkCmd.CreateParameter();
        p1.ParameterName = "@tableName";
        p1.Value = tableName;
        checkCmd.Parameters.Add(p1);

        var p2 = checkCmd.CreateParameter();
        p2.ParameterName = "@columnName";
        p2.Value = columnName;
        checkCmd.Parameters.Add(p2);

        var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
        if (count == 0)
        {
            using var alterCmd = connection.CreateCommand();
#pragma warning disable CA2100 // Dynamic schema maintenance uses internally sanitized identifiers from trusted migrations
            alterCmd.CommandText = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnDefinition};";
#pragma warning restore CA2100
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Internal schema maintenance uses compile-time sanitized identifiers")]
    private async Task EnsureIndexExistsAsync(string tableName, string indexName, string columns, CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(tableName, cancellationToken);
        if (!tableExists) return;

        var connection = _db.Database.GetDbConnection();
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE name = @indexName AND object_id = OBJECT_ID(@tableName);";
        var p1 = checkCmd.CreateParameter();
        p1.ParameterName = "@indexName";
        p1.Value = indexName;
        checkCmd.Parameters.Add(p1);

        var p2 = checkCmd.CreateParameter();
        p2.ParameterName = "@tableName";
        p2.Value = tableName;
        checkCmd.Parameters.Add(p2);

        var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
        if (count == 0)
        {
            using var createCmd = connection.CreateCommand();
#pragma warning disable CA2100 // Dynamic schema maintenance uses internally sanitized identifiers from trusted migrations
            createCmd.CommandText = $"CREATE INDEX [{indexName}] ON [{tableName}] ({columns});";
#pragma warning restore CA2100
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureSoftDeleteColumnsAsync(CancellationToken cancellationToken)
    {
        string[] softDeletableTables = ["Meals", "ProgressPhotos", "Users", "Profiles", "Corrections", "Ledgers", "AiFeedbacks"];
        foreach (var table in softDeletableTables)
        {
            await EnsureColumnExistsAsync(table, "IsDeleted", "BIT NOT NULL DEFAULT 0", cancellationToken);
            await EnsureColumnExistsAsync(table, "DeletedAtUtc", "DATETIME2 NULL", cancellationToken);
        }
    }
}

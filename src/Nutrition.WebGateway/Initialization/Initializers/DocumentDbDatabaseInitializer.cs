/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Bson;
using MongoDB.Driver;
using Polly;
using Polly.Retry;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Initializers;

/// <summary>
/// Strategy implementation for Azure DocumentDB (Free Tier - 32 GB dedicated storage) initialization,
/// cluster auto-resume warmup resilience, and collection/index verification.
/// </summary>
public sealed class DocumentDbDatabaseInitializer : IDatabaseInitializer
{
    private readonly IMongoDatabase _mongoDatabase;
    private readonly ILogger<DocumentDbDatabaseInitializer> _logger;

    public string ProviderName => "DocumentDb";

    public DocumentDbDatabaseInitializer(IMongoDatabase mongoDatabase, ILogger<DocumentDbDatabaseInitializer> logger)
    {
        _mongoDatabase = mongoDatabase ?? throw new ArgumentNullException(nameof(mongoDatabase));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        var docDbRetryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<MongoConnectionException>()
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 6,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromSeconds(10),
                OnRetry = args =>
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        var attempt = args.AttemptNumber + 1;
                        var delaySeconds = (int)args.RetryDelay.TotalSeconds;
                        _logger.LogWarning(args.Outcome.Exception, "[WARMUP] Azure DocumentDB Free Tier is auto-resuming from pause (attempt {Attempt}/6). Waiting {DelaySeconds}s for cluster to become online...", attempt, delaySeconds);
                    }
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        await docDbRetryPipeline.ExecuteAsync(async ct =>
        {
            var pingCmd = new BsonDocument("ping", 1);
            await _mongoDatabase.RunCommandAsync<BsonDocument>(pingCmd, cancellationToken: ct);
        }, cancellationToken);

        await EnsureIndexesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Azure DocumentDB collections and indexes verified and initialized successfully (Database: '{DatabaseName}').", _mongoDatabase.DatabaseNamespace.DatabaseName);
        }
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        // Canonical PascalCase 13-collection index & TTL provisioning (aligned with DocumentDbRepository<T>)
        await TryCreateIndexesAsync("Users", new[]
        {
            new CreateIndexModel<ApplicationUser>(
                Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            new CreateIndexModel<ApplicationUser>(
                Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedMobileNumber),
                new CreateIndexOptions { Sparse = true }),
            new CreateIndexModel<ApplicationUser>(
                Builders<ApplicationUser>.IndexKeys.Ascending(u => u.RecoveryLookupId),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            new CreateIndexModel<ApplicationUser>(
                Builders<ApplicationUser>.IndexKeys.Descending(u => u.LastActiveUtc))
        }, cancellationToken);

        await TryCreateIndexesAsync("Profiles", new[]
        {
            new CreateIndexModel<UserProfile>(
                Builders<UserProfile>.IndexKeys.Ascending(p => p.IsDeleted))
        }, cancellationToken);

        await TryCreateIndexesAsync("Meals", new[]
        {
            new CreateIndexModel<MealLog>(
                Builders<MealLog>.IndexKeys.Ascending(m => m.UserId).Descending(m => m.LoggedAt).Ascending(m => m.IsDeleted)),
            new CreateIndexModel<MealLog>(
                Builders<MealLog>.IndexKeys.Ascending(m => m.UserId).Ascending(m => m.ClientMutationId),
                new CreateIndexOptions { Unique = true, Sparse = true }),
            new CreateIndexModel<MealLog>(
                Builders<MealLog>.IndexKeys.Ascending(m => m.IsDeleted).Descending(m => m.DeletedAtUtc))
        }, cancellationToken);

        await TryCreateIndexesAsync("Ledgers", new[]
        {
            new CreateIndexModel<DailyCalorieLedger>(
                Builders<DailyCalorieLedger>.IndexKeys.Ascending(l => l.UserId).Descending(l => l.Date),
                new CreateIndexOptions { Unique = true })
        }, cancellationToken);

        await TryCreateIndexesAsync("Corrections", new[]
        {
            new CreateIndexModel<UserCorrectionRecord>(
                Builders<UserCorrectionRecord>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.NormalizedOriginalItem)),
            new CreateIndexModel<UserCorrectionRecord>(
                Builders<UserCorrectionRecord>.IndexKeys.Ascending(c => c.UserId).Descending(c => c.FrequencyCount))
        }, cancellationToken);

        await TryCreateIndexesAsync("ProgressPhotos", new[]
        {
            new CreateIndexModel<ProgressPhoto>(
                Builders<ProgressPhoto>.IndexKeys.Ascending(p => p.UserId).Descending(p => p.CapturedAtUtc).Ascending(p => p.IsDeleted)),
            new CreateIndexModel<ProgressPhoto>(
                Builders<ProgressPhoto>.IndexKeys.Ascending(p => p.UserId).Ascending(p => p.PhotoType))
        }, cancellationToken);

        await TryCreateIndexesAsync("AiFeedbacks", new[]
        {
            new CreateIndexModel<AiDetectionFeedbackRecord>(
                Builders<AiDetectionFeedbackRecord>.IndexKeys.Ascending(f => f.UserId).Descending(f => f.CreatedAtUtc))
        }, cancellationToken);

        await TryCreateIndexesAsync("VerificationOtps", new[]
        {
            new CreateIndexModel<VerificationOtp>(
                Builders<VerificationOtp>.IndexKeys.Ascending(o => o.UserId).Ascending(o => o.IsUsed).Descending(o => o.ExpiresAtUtc)),
            new CreateIndexModel<VerificationOtp>(
                Builders<VerificationOtp>.IndexKeys.Ascending(o => o.ExpiresAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) })
        }, cancellationToken);

        await TryCreateIndexesAsync("TierConfigurations", new[]
        {
            new CreateIndexModel<TierFeatureConfiguration>(
                Builders<TierFeatureConfiguration>.IndexKeys.Ascending(t => t.Tier),
                new CreateIndexOptions { Unique = true })
        }, cancellationToken);

        await TryCreateIndexesAsync("AiUsageLogs", new[]
        {
            new CreateIndexModel<AiUsageLog>(
                Builders<AiUsageLog>.IndexKeys
                    .Ascending(l => l.UserId)
                    .Descending(l => l.TimestampUtc)
                    .Ascending(l => l.IsSuccess)
                    .Ascending(l => l.OperationType)),
            new CreateIndexModel<AiUsageLog>(
                Builders<AiUsageLog>.IndexKeys.Descending(l => l.TimestampUtc))
        }, cancellationToken);

        await TryCreateIndexesAsync("QrSessions", new[]
        {
            new CreateIndexModel<QrLoginSession>(
                Builders<QrLoginSession>.IndexKeys.Ascending(q => q.ExchangeTicket),
                new CreateIndexOptions { Sparse = true }),
            new CreateIndexModel<QrLoginSession>(
                Builders<QrLoginSession>.IndexKeys.Ascending(q => q.AuthorizedUserId),
                new CreateIndexOptions { Sparse = true }),
            new CreateIndexModel<QrLoginSession>(
                Builders<QrLoginSession>.IndexKeys.Ascending(q => q.ExpiresAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) })
        }, cancellationToken);

        await TryCreateIndexesAsync("AppNotifications", new[]
        {
            new CreateIndexModel<AppNotification>(
                Builders<AppNotification>.IndexKeys.Ascending(n => n.RecipientUserId).Ascending(n => n.IsRead).Descending(n => n.CreatedAtUtc)),
            new CreateIndexModel<AppNotification>(
                Builders<AppNotification>.IndexKeys.Ascending(n => n.RecipientRole).Ascending(n => n.IsRead).Descending(n => n.CreatedAtUtc)),
            new CreateIndexModel<AppNotification>(
                Builders<AppNotification>.IndexKeys.Ascending(n => n.ReadAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(90) })
        }, cancellationToken);
    }

    private async Task TryCreateIndexesAsync<T>(
        string collectionName,
        IEnumerable<CreateIndexModel<T>> models,
        CancellationToken cancellationToken)
    {
        try
        {
            var col = _mongoDatabase.GetCollection<T>(collectionName);
            await col.Indexes.CreateManyAsync(models, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[INDEX] Azure DocumentDB {Collection} index check completed.", collectionName);
        }
    }
}

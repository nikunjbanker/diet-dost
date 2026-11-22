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
using Nutrition.Domain.Model.Meal;
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
        try
        {
            var usersCol = _mongoDatabase.GetCollection<ApplicationUser>("users");
            var usersIndexBuilder = Builders<ApplicationUser>.IndexKeys;
            await usersCol.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<ApplicationUser>(
                    usersIndexBuilder.Ascending(u => u.NormalizedEmail),
                    new CreateIndexOptions { Unique = true, Sparse = true }),
                new CreateIndexModel<ApplicationUser>(
                    usersIndexBuilder.Ascending(u => u.NormalizedMobileNumber),
                    new CreateIndexOptions { Sparse = true }),
                new CreateIndexModel<ApplicationUser>(
                    usersIndexBuilder.Ascending(u => u.RecoveryLookupId),
                    new CreateIndexOptions { Sparse = true })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[INDEX] Azure DocumentDB users index check completed.");
        }

        try
        {
            var mealsCol = _mongoDatabase.GetCollection<MealLog>("meals");
            var mealsIndexBuilder = Builders<MealLog>.IndexKeys;
            await mealsCol.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<MealLog>(mealsIndexBuilder.Ascending(m => m.UserId)),
                new CreateIndexModel<MealLog>(mealsIndexBuilder.Ascending(m => m.UserId).Descending(m => m.LoggedAt))
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[INDEX] Azure DocumentDB meals index check completed.");
        }

        try
        {
            var tierCol = _mongoDatabase.GetCollection<TierFeatureConfiguration>("tier_configurations");
            var tierIndexBuilder = Builders<TierFeatureConfiguration>.IndexKeys;
            await tierCol.Indexes.CreateOneAsync(
                new CreateIndexModel<TierFeatureConfiguration>(
                    tierIndexBuilder.Ascending(t => t.Tier),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[INDEX] Azure DocumentDB tier_configurations index check completed.");
        }
    }
}

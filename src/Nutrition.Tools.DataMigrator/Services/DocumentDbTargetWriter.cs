/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using Nutrition.Tools.DataMigrator.Reporting;

namespace Nutrition.Tools.DataMigrator.Services;

/// <summary>
/// Target writer inserting and upserting domain records into Azure DocumentDB collections.
/// Operates natively on BSON documents with automatic indexing, batching, and progress reporting.
/// </summary>
public class DocumentDbTargetWriter : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private MongoClient? _client;
    private IMongoDatabase? _database;

    public DocumentDbTargetWriter(string connectionString, string databaseName = "dietdost")
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _databaseName = databaseName;
    }

    public Task OpenAsync()
    {
        var mongoUrl = new MongoUrl(_connectionString);
        var settings = MongoClientSettings.FromUrl(mongoUrl);
        settings.ConnectTimeout = TimeSpan.FromSeconds(60);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(60);
        settings.MaxConnectionIdleTime = TimeSpan.FromSeconds(120);

        _client = new MongoClient(settings);
        var dbName = !string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? mongoUrl.DatabaseName : _databaseName;
        _database = _client.GetDatabase(dbName);
        return Task.CompletedTask;
    }

    public async Task WarmupAndVerifyAsync()
    {
        EnsureConnected();
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                var command = new BsonDocument("ping", 1);
                await _database!.RunCommandAsync<BsonDocument>(command);
                return;
            }
            catch (Exception ex) when (attempt < 4)
            {
                Console.WriteLine($"[WARMUP] Azure DocumentDB cluster is auto-resuming from dormancy (attempt {attempt}/4): {ex.Message}. Waiting 15s...");
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }

    public async Task EnsureSchemaForTableAsync(string tableName)
    {
        EnsureConnected();
        if (tableName.Equals("FoodItems", StringComparison.OrdinalIgnoreCase))
        {
            // FoodItems are embedded inside Meals.Items in DocumentDB (no standalone FoodItems collection).
            return;
        }

        var collectionName = ResolveCollectionName(tableName);
        var collectionsCursor = await _database!.ListCollectionNamesAsync();
        var collections = await collectionsCursor.ToListAsync();

        if (!collections.Contains(collectionName, StringComparer.OrdinalIgnoreCase))
        {
            await _database.CreateCollectionAsync(collectionName);
        }

        var collection = _database.GetCollection<BsonDocument>(collectionName);

        // Build indexes based on table domain characteristics
        if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            var emailIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("NormalizedEmail"),
                new CreateIndexOptions { Unique = true, Sparse = true });
            var mobileIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("NormalizedMobileNumber"));
            await collection.Indexes.CreateManyAsync([emailIndex, mobileIndex]);
        }
        else if (tableName.Equals("Meals", StringComparison.OrdinalIgnoreCase))
        {
            var userIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("UserId").Descending("LoggedAt"));
            await collection.Indexes.CreateOneAsync(userIndex);
        }
        else if (tableName.Equals("Ledgers", StringComparison.OrdinalIgnoreCase))
        {
            var ledgerIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("UserId").Ascending("Date"));
            await collection.Indexes.CreateOneAsync(ledgerIndex);
        }
        else if (tableName.Equals("ProgressPhotos", StringComparison.OrdinalIgnoreCase))
        {
            var photoIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("UserId").Descending("CapturedAtUtc"));
            await collection.Indexes.CreateOneAsync(photoIndex);
        }
        else if (tableName.Equals("TierConfigurations", StringComparison.OrdinalIgnoreCase))
        {
            var tierIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("Tier"),
                new CreateIndexOptions { Unique = true });
            await collection.Indexes.CreateOneAsync(tierIndex);
        }
        else if (tableName.Equals("AiUsageLogs", StringComparison.OrdinalIgnoreCase))
        {
            var logIndex = new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("UserId").Descending("TimestampUtc"));
            await collection.Indexes.CreateOneAsync(logIndex);
        }
    }

    public async Task<TableMigrationResult> MigrateTableAsync(
        SqliteTableMetadata tbl,
        IAsyncEnumerable<Dictionary<string, object?>> rows,
        DataConversionEngine engine,
        MigrationMode mode,
        bool isDryRun,
        Action<int>? onRowProgress = null)
    {
        EnsureConnected();
        var stopwatch = Stopwatch.StartNew();
        var isEmbeddedFoodItems = tbl.TableName.Equals("FoodItems", StringComparison.OrdinalIgnoreCase);
        var collectionName = isEmbeddedFoodItems ? "Meals" : ResolveCollectionName(tbl.TableName);
        var collection = _database!.GetCollection<BsonDocument>(collectionName);
        var stats = new TableConversionStats { TableName = tbl.TableName };
        var errors = new List<string>();

        var beforeCount = (int)await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        int inserted = 0;
        int updated = 0;
        int skipped = 0;
        int processed = 0;

        if (mode == MigrationMode.CleanInsert && !isDryRun)
        {
            await collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            beforeCount = 0;
        }

        var batch = new List<BsonDocument>(50);
        await foreach (var row in rows)
        {
            processed++;
            stats.TotalRowsRead++;
            onRowProgress?.Invoke(processed);

            var rowKey = tbl.PrimaryKeyColumns.Count > 0 && row.TryGetValue(tbl.PrimaryKeyColumns[0], out var pkVal)
                ? pkVal?.ToString() ?? $"row_{processed}"
                : $"row_{processed}";

            try
            {
                var doc = engine.ConvertToBsonDocument(tbl.TableName, row, rowKey, stats);
                batch.Add(doc);

                if (batch.Count >= 50)
                {
                    if (!isDryRun)
                    {
                        var (ins, upd, skp) = isEmbeddedFoodItems
                            ? await EmbedFoodItemsBatchAsync(collection, batch)
                            : await WriteBatchAsync(collection, batch, mode);
                        inserted += ins;
                        updated += upd;
                        skipped += skp;
                    }
                    else
                    {
                        inserted += batch.Count;
                    }
                    batch.Clear();
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Row {rowKey} conversion failed: {ex.Message}");
            }
        }

        if (batch.Count > 0)
        {
            if (!isDryRun)
            {
                var (ins, upd, skp) = isEmbeddedFoodItems
                    ? await EmbedFoodItemsBatchAsync(collection, batch)
                    : await WriteBatchAsync(collection, batch, mode);
                inserted += ins;
                updated += upd;
                skipped += skp;
            }
            else
            {
                inserted += batch.Count;
            }
            batch.Clear();
        }

        stopwatch.Stop();
        var afterCount = isDryRun
            ? beforeCount + inserted
            : (int)await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        return new TableMigrationResult(
            tbl.TableName,
            processed,
            beforeCount,
            inserted,
            updated,
            skipped,
            afterCount,
            stats,
            errors,
            stopwatch.Elapsed
        );
    }

    private static async Task<(int inserted, int updated, int skipped)> WriteBatchAsync(
        IMongoCollection<BsonDocument> collection,
        List<BsonDocument> batch,
        MigrationMode mode)
    {
        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        if (mode == MigrationMode.CleanInsert)
        {
            await collection.InsertManyAsync(batch);
            inserted += batch.Count;
            return (inserted, updated, skipped);
        }

        HashSet<BsonValue>? existingIds = null;
        if (mode == MigrationMode.SkipExisting && batch.Count > 0)
        {
            var batchIds = batch.Select(d => d["_id"]).ToList();
            var existingDocs = await collection.Find(Builders<BsonDocument>.Filter.In("_id", batchIds))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .ToListAsync();
            existingIds = existingDocs.Select(d => d["_id"]).ToHashSet();
        }

        var writeModels = new List<WriteModel<BsonDocument>>(batch.Count);
        foreach (var doc in batch)
        {
            var id = doc["_id"];
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id);

            if (mode == MigrationMode.Upsert)
            {
                var replaceModel = new ReplaceOneModel<BsonDocument>(filter, doc) { IsUpsert = true };
                writeModels.Add(replaceModel);
            }
            else if (mode == MigrationMode.SkipExisting)
            {
                if (existingIds == null || !existingIds.Contains(id))
                {
                    writeModels.Add(new InsertOneModel<BsonDocument>(doc));
                }
                else
                {
                    skipped++;
                }
            }
        }

        if (writeModels.Count > 0)
        {
            var bulkResult = await collection.BulkWriteAsync(writeModels, new BulkWriteOptions { IsOrdered = false });
            inserted += (int)bulkResult.InsertedCount + (int)bulkResult.Upserts.Count;
            updated += (int)bulkResult.ModifiedCount;
        }

        return (inserted, updated, skipped);
    }

    private static async Task<(int inserted, int updated, int skipped)> EmbedFoodItemsBatchAsync(
        IMongoCollection<BsonDocument> mealsCollection,
        List<BsonDocument> foodItemBatch)
    {
        int updated = 0;
        int skipped = 0;
        var writeModels = new List<WriteModel<BsonDocument>>(foodItemBatch.Count);

        foreach (var item in foodItemBatch)
        {
            if (!item.TryGetValue("MealLogId", out var mealLogId) || mealLogId.IsBsonNull)
            {
                skipped++;
                continue;
            }

            var filter = Builders<BsonDocument>.Filter.Eq("_id", mealLogId);
            var update = Builders<BsonDocument>.Update.AddToSet("Items", item);
            writeModels.Add(new UpdateOneModel<BsonDocument>(filter, update));
        }

        if (writeModels.Count > 0)
        {
            var bulkResult = await mealsCollection.BulkWriteAsync(writeModels, new BulkWriteOptions { IsOrdered = false });
            updated += (int)bulkResult.ModifiedCount;
        }

        return (0, updated, skipped);
    }

    private static string ResolveCollectionName(string tableName)
    {
        return tableName switch
        {
            "Profiles" => "Profiles",
            "Meals" => "Meals",
            "FoodItems" => "FoodItems",
            "Ledgers" => "Ledgers",
            "Corrections" => "Corrections",
            "ProgressPhotos" => "ProgressPhotos",
            "AiFeedbacks" => "AiFeedbacks",
            "Users" => "Users",
            "VerificationOtps" => "VerificationOtps",
            "TierConfigurations" => "TierConfigurations",
            "AiUsageLogs" => "AiUsageLogs",
            "AppSecrets" => "AppSecrets",
            "QrSessions" => "QrSessions",
            "AppNotifications" => "AppNotifications",
            _ => tableName
        };
    }

    private void EnsureConnected()
    {
        if (_database == null)
        {
            throw new InvalidOperationException("DocumentDB client is not connected. Call OpenAsync() first.");
        }
    }

    public void Dispose()
    {
        _client = null;
        _database = null;
        GC.SuppressFinalize(this);
    }
}

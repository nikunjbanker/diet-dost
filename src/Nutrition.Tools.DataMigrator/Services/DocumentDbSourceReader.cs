/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Bson;
using MongoDB.Driver;

namespace Nutrition.Tools.DataMigrator.Services;

/// <summary>
/// Source reader extracting domain documents from Azure DocumentDB collections.
/// Enables streaming records out of DocumentDB into Azure SQL or SQLite.
/// </summary>
public class DocumentDbSourceReader : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly DataConversionEngine _engine;
    private MongoClient? _client;
    private IMongoDatabase? _database;

    private static readonly List<string> KnownDomainCollections = new()
    {
        "Users",
        "Profiles",
        "Meals",
        "FoodItems",
        "Ledgers",
        "ProgressPhotos",
        "Corrections",
        "AiFeedbacks",
        "TierConfigurations",
        "AiUsageLogs",
        "AppSecrets",
        "VerificationOtps",
        "QrSessions",
        "AppNotifications"
    };

    public DocumentDbSourceReader(string connectionString, string databaseName = "dietdost", DataConversionEngine? engine = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _databaseName = databaseName;
        _engine = engine ?? new DataConversionEngine();
    }

    public bool IsDomainCollection(string collectionName)
    {
        return KnownDomainCollections.Contains(collectionName, StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, object?> ConvertBsonToRow(BsonDocument doc, string tableName)
    {
        return _engine.ConvertFromBsonDocument(tableName, doc);
    }

    public Task OpenAsync()
    {
        var mongoUrl = new MongoUrl(_connectionString);
        var settings = MongoClientSettings.FromUrl(mongoUrl);
        settings.ConnectTimeout = TimeSpan.FromSeconds(60);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(60);

        _client = new MongoClient(settings);
        var dbName = !string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? mongoUrl.DatabaseName : _databaseName;
        _database = _client.GetDatabase(dbName);
        return Task.CompletedTask;
    }

    public async Task<List<SqliteTableMetadata>> DiscoverTablesAsync()
    {
        EnsureConnected();
        var tables = new List<SqliteTableMetadata>();
        var collectionsCursor = await _database!.ListCollectionNamesAsync();
        var collections = await collectionsCursor.ToListAsync();

        foreach (var colName in KnownDomainCollections)
        {
            if (collections.Contains(colName, StringComparer.OrdinalIgnoreCase))
            {
                var collection = _database.GetCollection<BsonDocument>(colName);
                var count = (int)await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                var pkName = colName.Equals("AppSecrets", StringComparison.OrdinalIgnoreCase) ? "Key" : "Id";

                var pkCol = new SqliteColumnMetadata(0, pkName, "TEXT", true, null, true);
                tables.Add(new SqliteTableMetadata(colName, count, new List<SqliteColumnMetadata> { pkCol }, new List<string> { pkName }));
            }
        }

        return tables;
    }

    public async IAsyncEnumerable<Dictionary<string, object?>> StreamRowsAsync(string tableName)
    {
        EnsureConnected();
        var collection = _database!.GetCollection<BsonDocument>(tableName);
        using var cursor = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();

        while (await cursor.MoveNextAsync())
        {
            foreach (var doc in cursor.Current)
            {
                yield return _engine.ConvertFromBsonDocument(tableName, doc);
            }
        }
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

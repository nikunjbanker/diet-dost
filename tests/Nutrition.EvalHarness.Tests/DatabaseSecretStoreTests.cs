/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Configuration;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DatabaseSecretStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly DatabaseSecretStore _secretStore;

    public DatabaseSecretStoreTests()
    {
        DatabaseSecretStore.ClearCache();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        _secretStore = new DatabaseSecretStore(_db, NullLogger<DatabaseSecretStore>.Instance);
    }

    public void Dispose()
    {
        DatabaseSecretStore.ClearCache();
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetSecretAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _secretStore.GetSecretAsync("NonExistent:Key");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetSecretAsync_NewSecret_PersistsToDatabaseAndCache()
    {
        const string key = "Test:ApiKey";
        const string val = "secret-token-xyz-123456";

        await _secretStore.SetSecretAsync(key, val, "Test API Token");

        var retrieved = await _secretStore.GetSecretAsync(key);
        Assert.Equal(val, retrieved);

        var directDb = await _db.AppSecrets.FirstOrDefaultAsync(s => s.Key == key);
        Assert.NotNull(directDb);
        Assert.Equal(val, directDb.Value);
        Assert.Equal("Test API Token", directDb.Description);
    }

    [Fact]
    public async Task SetSecretAsync_ExistingSecret_UpdatesValue()
    {
        const string key = "Jwt:SigningSecret";
        await _secretStore.SetSecretAsync(key, "old-secret-value-32-chars-long!", "Initial Description");

        await _secretStore.SetSecretAsync(key, "new-secret-value-32-chars-long!", "Updated Description");

        var updated = await _secretStore.GetSecretAsync(key);
        Assert.Equal("new-secret-value-32-chars-long!", updated);
    }

    [Fact]
    public async Task GetRequiredSecretAsync_MissingKey_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _secretStore.GetRequiredSecretAsync("Required:MissingKey"));
    }

    [Fact]
    public async Task GetAllSecretsAsync_ReturnsAllPersistedSecrets()
    {
        await _secretStore.SetSecretAsync("Key1", "Val1");
        await _secretStore.SetSecretAsync("Key2", "Val2");

        var all = await _secretStore.GetAllSecretsAsync();
        Assert.True(all.ContainsKey("Key1"));
        Assert.True(all.ContainsKey("Key2"));
        Assert.Equal("Val1", all["Key1"]);
        Assert.Equal("Val2", all["Key2"]);
    }

    [Fact]
    public void DatabaseConfigurationProvider_SeedsDefaultsAndPopulatesConfiguration()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"secrets_test_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={tempDbPath}";

        try
        {
            var config = new ConfigurationBuilder()
                .AddDatabaseSecrets(connStr)
                .Build();

            // Default secrets should have been seeded into SQLite and exposed via IConfiguration
            var jwtKey = config[SecretKeys.JwtSigningKey];
            var demoPassword = config[SecretKeys.DemoPassword];

            Assert.NotNull(jwtKey);
            Assert.True(jwtKey.Length >= 32);
            Assert.Equal("DietDost@Demo2026!", demoPassword);

            // JwtTokenService should successfully initialize using the key loaded from database
            var jwtService = new JwtTokenService(config);
            Assert.NotNull(jwtService);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { /* ignore */ }
            }
        }
    }
}

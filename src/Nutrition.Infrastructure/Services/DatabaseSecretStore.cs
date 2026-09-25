using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Database-backed implementation of ISecretStore.
/// Reads and writes application secrets directly to the AppSecrets database table.
/// Employs a thread-safe concurrent in-memory cache to ensure near-zero latency on hot read paths.
/// </summary>
public class DatabaseSecretStore : ISecretStore
{
    private readonly DietTrackerDbContext _db;
    private readonly ILogger<DatabaseSecretStore> _logger;
    private static readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseSecretStore(DietTrackerDbContext db, ILogger<DatabaseSecretStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static void ClearCache() => _cache.Clear();

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_cache.TryGetValue(key, out var cachedVal))
            return cachedVal;

        var secret = await _db.AppSecrets.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (secret != null)
        {
            _cache[key] = secret.Value;
            return secret.Value;
        }

        return null;
    }

    public async Task<string> GetRequiredSecretAsync(string key, CancellationToken ct = default)
    {
        var secret = await GetSecretAsync(key, ct);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new KeyNotFoundException($"Required secret '{key}' was not found in the AppSecrets database store.");
        }
        return secret;
    }

    public async Task SetSecretAsync(string key, string value, string? description = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var existing = await _db.AppSecrets.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing == null)
        {
            var newSecret = new AppSecret
            {
                Key = key.Trim(),
                Value = value,
                Description = description,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _db.AppSecrets.AddAsync(newSecret, ct);
        }
        else
        {
            existing.Value = value;
            if (description != null) existing.Description = description;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            _db.AppSecrets.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
        _cache[key] = value;
        _logger.LogInformation("Updated database secret for key: {Key}", key);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken ct = default)
    {
        var list = await _db.AppSecrets.AsNoTracking().ToListAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
        {
            dict[s.Key] = s.Value;
            _cache[s.Key] = s.Value;
        }
        return dict;
    }
}

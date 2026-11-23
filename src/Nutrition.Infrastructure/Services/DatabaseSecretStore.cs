/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Database and Configuration-backed implementation of ISecretStore.
/// Seamlessly checks strongly-typed Options, IConfiguration, and falls back to AppSecrets repository.
/// Decoupled from EF Core via IRepository and IUnitOfWork abstractions (Clean Architecture & DIP).
/// Employs a thread-safe concurrent in-memory cache to ensure near-zero latency on hot read paths.
/// </summary>
public class DatabaseSecretStore : ISecretStore
{
    private readonly IRepository<AppSecret> _secretRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DatabaseSecretStore> _logger;
    private readonly IConfiguration? _configuration;
    private readonly IOptions<JwtOptions>? _jwtOptions;
    private readonly IOptions<AuthOptions>? _authOptions;
    private readonly IOptions<AiOptions>? _aiOptions;
    private static readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseSecretStore(
        IRepository<AppSecret> secretRepo,
        IUnitOfWork unitOfWork,
        ILogger<DatabaseSecretStore> logger,
        IConfiguration? configuration = null,
        IOptions<JwtOptions>? jwtOptions = null,
        IOptions<AuthOptions>? authOptions = null,
        IOptions<AiOptions>? aiOptions = null)
    {
        _secretRepo = secretRepo ?? throw new ArgumentNullException(nameof(secretRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration;
        _jwtOptions = jwtOptions;
        _authOptions = authOptions;
        _aiOptions = aiOptions;
    }

    /// <summary>
    /// Backwards-compatible internal constructor for test fixtures.
    /// Internal so DI container never sees ambiguous public constructors.
    /// </summary>
    internal DatabaseSecretStore(
        DietTrackerDbContext db,
        ILogger<DatabaseSecretStore> logger,
        IConfiguration? configuration = null,
        IOptions<JwtOptions>? jwtOptions = null,
        IOptions<AuthOptions>? authOptions = null,
        IOptions<AiOptions>? aiOptions = null)
        : this(new EfRepository<AppSecret>(db, NullLogger<EfRepository<AppSecret>>.Instance),
               new EfUnitOfWork(db, NullLogger<EfUnitOfWork>.Instance),
               logger,
               configuration,
               jwtOptions,
               authOptions,
               aiOptions)
    {
    }

    public static void ClearCache() => _cache.Clear();

    public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        // 1. Check strongly-typed options first
        if (key.Equals("Jwt:Key", StringComparison.OrdinalIgnoreCase) || key.Equals("Jwt__Key", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_jwtOptions?.Value?.Key))
                return _jwtOptions.Value.Key;
        }
        else if (key.Equals("Auth:SuperAdminEmail", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_authOptions?.Value?.SuperAdminEmail))
                return _authOptions.Value.SuperAdminEmail;
        }
        else if (key.Equals("AI:GoogleAI:ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_aiOptions?.Value?.GoogleAI?.ApiKey))
                return _aiOptions.Value.GoogleAI.ApiKey;
        }
        else if (key.Equals("AI:AzureOpenAI:ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_aiOptions?.Value?.AzureOpenAI?.ApiKey))
                return _aiOptions.Value.AzureOpenAI.ApiKey;
        }

        // 2. Prioritize Azure Key Vault / Environment configuration if present
        if (_configuration != null)
        {
            var configVal = _configuration.GetValue<string>(key);
            if (!string.IsNullOrWhiteSpace(configVal))
            {
                return configVal;
            }
        }

        // 3. Check local in-memory cache
        if (_cache.TryGetValue(key, out var cachedVal))
            return cachedVal;

        // 4. Fall back to AppSecrets database repository
        var secret = await _secretRepo.FirstOrDefaultAsync(s => s.Key == key, ct);
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

        var existing = await _secretRepo.FirstOrDefaultAsync(s => s.Key == key, ct);
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
            await _secretRepo.AddAsync(newSecret, ct);
        }
        else
        {
            existing.Value = value;
            if (description != null) existing.Description = description;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await _secretRepo.UpdateAsync(existing, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _cache[key] = value;
        _logger.LogInformation("Updated database secret for key: {Key}", key);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken ct = default)
    {
        var list = await _secretRepo.GetAllAsync(ct);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in list)
        {
            dict[s.Key] = s.Value;
            _cache[s.Key] = s.Value;
        }
        return dict;
    }
}

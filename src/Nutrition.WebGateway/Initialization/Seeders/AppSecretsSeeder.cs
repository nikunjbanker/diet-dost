/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Domain.Model.Security;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Seeders;

/// <summary>
/// Ordered seeder initializing local cryptographic and API keys into the AppSecrets repository for Development environments.
/// Strictly skipped in Release and Production environments (GAP-18 Sole Authority - Key Vault enforcement).
/// </summary>
public sealed class AppSecretsSeeder : IDatabaseSeeder
{
    private readonly IRepository<AppSecret> _secretRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogger<AppSecretsSeeder> _logger;

    public int Order => 10;

    public AppSecretsSeeder(
        IRepository<AppSecret> secretRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IAppEnvironment appEnvironment,
        ILogger<AppSecretsSeeder> logger)
    {
        _secretRepository = secretRepository ?? throw new ArgumentNullException(nameof(secretRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnvironment = appEnvironment ?? throw new ArgumentNullException(nameof(appEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_appEnvironment.IsDevelopment)
        {
            // GAP-18: Sole Authority - Do not copy Key Vault secrets to database AppSecrets store in production
            return;
        }

        var defaultSecrets = new List<(string Key, string FallbackValue, string Description)>();
        var jwtOptions = _configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var aiOptions = _configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        var authOptions = _configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        var devJwtKey = !string.IsNullOrWhiteSpace(jwtOptions.Key)
            ? jwtOptions.Key
            : "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";

        defaultSecrets.Add(("Jwt:Key", devJwtKey, "Cryptographic signing key for JWT HMAC-SHA256 tokens (Development Only)"));
        defaultSecrets.Add(("AI:GoogleAI:ApiKey", aiOptions.GoogleAI?.ApiKey ?? string.Empty, "Google Gemini Vision API Key"));
        defaultSecrets.Add(("AI:AzureOpenAI:ApiKey", aiOptions.AzureOpenAI?.ApiKey ?? string.Empty, "Azure OpenAI API Key"));

        if (_appEnvironment.AllowsDemoUsers)
        {
            defaultSecrets.Add(("Auth:DemoPassword", authOptions.DemoPassword, "Deterministic password for seeded demo tier accounts (Debug/Dev only)"));
        }

        bool mutated = false;
        foreach (var (key, fallbackValue, description) in defaultSecrets)
        {
            var existing = await _secretRepository.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (existing == null)
            {
                await _secretRepository.AddAsync(new AppSecret
                {
                    Key = key,
                    Value = fallbackValue,
                    Description = description,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }, cancellationToken);
                mutated = true;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Seeded database secret into AppSecrets: {Key}", key);
                }
            }
        }

        if (mutated)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            (_configuration as IConfigurationRoot)?.Reload();
        }
    }
}

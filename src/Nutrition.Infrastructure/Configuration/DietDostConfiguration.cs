/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;

namespace Nutrition.Infrastructure.Configuration;

/// <summary>
/// Authoritative implementation of IDietDostConfiguration.
/// Bridges ASP.NET Core IOptions{T} strongly-typed groups and IConfiguration
/// to expose validated settings, fallback defaults, and normalized secrets.
/// </summary>
public sealed class DietDostConfiguration : IDietDostConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly IAppEnvironment? _appEnv;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthOptions _authOptions;
    private readonly AiOptions _aiOptions;
    private readonly DatabaseOptions _databaseOptions;

    public const string DefaultDevJwtKey = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";
    public const string DefaultSuperAdminEmail = "superadmin@dietdost.app";

    public DietDostConfiguration(
        IOptions<JwtOptions> jwtOptions,
        IOptions<AuthOptions> authOptions,
        IOptions<AiOptions> aiOptions,
        IOptions<DatabaseOptions> dbOptions,
        IConfiguration configuration,
        IAppEnvironment? appEnv = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnv = appEnv;
        _jwtOptions = jwtOptions?.Value ?? configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        _authOptions = authOptions?.Value ?? configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        _aiOptions = aiOptions?.Value ?? configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        _databaseOptions = dbOptions?.Value ?? configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    }

    public DietDostConfiguration(IConfiguration configuration, IAppEnvironment? appEnv = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnv = appEnv;
        _jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        _authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        _aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        _databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
    }

    public string JwtKey =>
        !string.IsNullOrWhiteSpace(_jwtOptions.Key)
            ? _jwtOptions.Key
            : (_appEnv?.IsDevelopment == true || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
                ? DefaultDevJwtKey
                : throw new InvalidOperationException("CRITICAL CONFIGURATION ERROR: 'Jwt:Key' is not configured."));

    public string JwtIssuer =>
        !string.IsNullOrWhiteSpace(_jwtOptions.Issuer) ? _jwtOptions.Issuer : "DietDostGateway";

    public string JwtAudience =>
        !string.IsNullOrWhiteSpace(_jwtOptions.Audience) ? _jwtOptions.Audience : "DietDostClient";

    public int JwtExpiryMinutes =>
        _jwtOptions.ExpiryMinutes > 0 ? _jwtOptions.ExpiryMinutes : 1440;

    public string SuperAdminEmail =>
        !string.IsNullOrWhiteSpace(_authOptions.SuperAdminEmail)
            ? _authOptions.SuperAdminEmail
            : (_appEnv?.IsDevelopment == true || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
                ? DefaultSuperAdminEmail
                : throw new InvalidOperationException("CRITICAL CONFIGURATION ERROR: 'Auth:SuperAdminEmail' is not configured."));

    public bool RequireMobileVerification => _authOptions.RequireMobileVerification;

    public bool AllowRegistration => _authOptions.AllowRegistration;

    public string DefaultConnection =>
        !string.IsNullOrWhiteSpace(_databaseOptions.ConnectionString)
            ? _databaseOptions.ConnectionString
            : _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";

    public string DatabaseProvider =>
        !string.IsNullOrWhiteSpace(_databaseOptions.Provider) ? _databaseOptions.Provider : "Sqlite";

    public string AiProvider =>
        !string.IsNullOrWhiteSpace(_aiOptions.Provider) ? _aiOptions.Provider : "GoogleAI";

    public string GoogleAiModelId =>
        !string.IsNullOrWhiteSpace(_aiOptions.GoogleAI?.ModelId) ? _aiOptions.GoogleAI.ModelId : "gemini-3-flash-preview";

    public string GoogleAiFallbackModelId =>
        !string.IsNullOrWhiteSpace(_aiOptions.GoogleAI?.FallbackModelId) ? _aiOptions.GoogleAI.FallbackModelId : "gemini-3.6-flash";

    public string? GoogleAiEndpoint => _aiOptions.GoogleAI?.Endpoint;

    public string? GoogleAiApiKey => _aiOptions.GoogleAI?.ApiKey;

    public string AzureOpenAiDeploymentName =>
        !string.IsNullOrWhiteSpace(_aiOptions.AzureOpenAI?.DeploymentName) ? _aiOptions.AzureOpenAI.DeploymentName : "gpt-5.6-luna";

    public string? AzureOpenAiEndpoint => _aiOptions.AzureOpenAI?.Endpoint;

    public string? AzureOpenAiApiKey => _aiOptions.AzureOpenAI?.ApiKey;

    public int AiMaxTokens => _aiOptions.MaxTokens > 0 ? _aiOptions.MaxTokens : 8192;

    public double AiTemperature => _aiOptions.Temperature;

    public bool AiShowModelDetails => _aiOptions.ShowModelDetails;

    public string? this[string key] => _configuration[key];
}

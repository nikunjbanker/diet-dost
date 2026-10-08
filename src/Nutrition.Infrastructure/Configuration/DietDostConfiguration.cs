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
    private readonly JwtOptions? _jwtOptions;
    private readonly AuthOptions? _authOptions;
    private readonly AiOptions? _aiOptions;
    private readonly DatabaseOptions? _databaseOptions;

    public const string DefaultDevJwtKey = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";
    public const string DefaultSuperAdminEmail = "superadmin@dietdost.app";

    public DietDostConfiguration(
        IOptions<JwtOptions> jwtOptions,
        IOptions<AuthOptions> authOptions,
        IOptions<AiOptions> aiOptions,
        IOptions<DatabaseOptions> dbOptions,
        IConfiguration configuration,
        IAppEnvironment? appEnv = null)
        : this(configuration, appEnv)
    {
        _jwtOptions = jwtOptions?.Value;
        _authOptions = authOptions?.Value;
        _aiOptions = aiOptions?.Value;
        _databaseOptions = dbOptions?.Value;
    }

    public DietDostConfiguration(IConfiguration configuration, IAppEnvironment? appEnv = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnv = appEnv;
    }

    public string JwtKey =>
        !string.IsNullOrWhiteSpace(_jwtOptions?.Key)
            ? _jwtOptions.Key
            : (_configuration["Jwt:Key"]
               ?? _configuration["JWT_KEY"]
               ?? _configuration["Jwt__Key"]
               ?? (_appEnv?.IsDevelopment == true || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
                   ? DefaultDevJwtKey
                   : throw new InvalidOperationException("CRITICAL CONFIGURATION ERROR: 'Jwt:Key' is not configured.")));

    public string JwtIssuer =>
        _jwtOptions?.Issuer ?? _configuration["Jwt:Issuer"] ?? "DietDostGateway";

    public string JwtAudience =>
        _jwtOptions?.Audience ?? _configuration["Jwt:Audience"] ?? "DietDostClient";

    public int JwtExpiryMinutes =>
        _jwtOptions?.ExpiryMinutes ?? (int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) && exp > 0 ? exp : 1440);

    public string SuperAdminEmail =>
        _authOptions?.SuperAdminEmail
        ?? _configuration["Auth:SuperAdminEmail"]
        ?? _configuration["SuperAdminEmail"]
        ?? Environment.GetEnvironmentVariable("SUPER_ADMIN_EMAIL")
        ?? DefaultSuperAdminEmail;

    public bool RequireMobileVerification =>
        _authOptions?.RequireMobileVerification
        ?? (_configuration.GetValue<bool>("Auth:RequireMobileVerification", false)
            || _configuration.GetValue<bool>("RequireMobileVerification", false));

    public bool AllowRegistration =>
        _authOptions?.AllowRegistration
        ?? _configuration.GetValue<bool>("Auth:AllowRegistration", true);

    public string DefaultConnection =>
        _databaseOptions?.ConnectionString
        ?? _configuration.GetConnectionString("DefaultConnection")
        ?? _configuration["ConnectionStrings:DefaultConnection"]
        ?? "Data Source=diettracker.db";

    public string DatabaseProvider =>
        _databaseOptions?.Provider
        ?? _configuration["Database:Provider"]
        ?? "Sqlite";

    public string AiProvider =>
        _aiOptions?.Provider ?? _configuration["AI:Provider"] ?? "GoogleAI";

    public string GoogleAiModelId =>
        _aiOptions?.GoogleAI?.ModelId
        ?? _configuration["AI:GoogleAI:ModelId"]
        ?? _configuration["AI:ModelId"]
        ?? "gemini-3-flash-preview";

    public string GoogleAiFallbackModelId =>
        _aiOptions?.GoogleAI?.FallbackModelId
        ?? _configuration["AI:GoogleAI:FallbackModelId"]
        ?? _configuration["AI:FallbackModelId"]
        ?? "gemini-3.6-flash";

    public string? GoogleAiEndpoint =>
        _aiOptions?.GoogleAI?.Endpoint
        ?? _configuration["AI:GoogleAI:Endpoint"]
        ?? _configuration["AI:Endpoint"]
        ?? "https://generativelanguage.googleapis.com/v1beta/models";

    public string? GoogleAiApiKey =>
        _aiOptions?.GoogleAI?.ApiKey
        ?? _configuration["AI:GoogleAI:ApiKey"]
        ?? _configuration["AI:ApiKey"]
        ?? _configuration["Gemini:ApiKey"]
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    public string AzureOpenAiDeploymentName =>
        _aiOptions?.AzureOpenAI?.DeploymentName
        ?? _configuration["AI:AzureOpenAI:DeploymentName"]
        ?? "gpt-5.6-luna";

    public string? AzureOpenAiEndpoint =>
        _aiOptions?.AzureOpenAI?.Endpoint
        ?? _configuration["AI:AzureOpenAI:Endpoint"]
        ?? "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1";

    public string? AzureOpenAiApiKey =>
        _aiOptions?.AzureOpenAI?.ApiKey
        ?? _configuration["AI:AzureOpenAI:ApiKey"]
        ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

    public int AiMaxTokens =>
        _aiOptions?.MaxTokens ?? (int.TryParse(_configuration["AI:MaxTokens"], out var mt) && mt > 0 ? mt : 8192);

    public double AiTemperature =>
        _aiOptions?.Temperature ?? (double.TryParse(_configuration["AI:Temperature"], out var temp) ? temp : 0.2);

    public bool AiShowModelDetails =>
        _aiOptions?.ShowModelDetails ?? (!bool.TryParse(_configuration["AI:ShowModelDetails"], out var show) || show);

    public string? this[string key] => _configuration[key];
}

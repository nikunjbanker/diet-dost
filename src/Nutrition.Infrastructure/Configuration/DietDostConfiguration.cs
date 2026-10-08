/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Configuration;

/// <summary>
/// Authoritative implementation of IDietDostConfiguration.
/// Wraps ASP.NET Core IConfiguration to expose strongly-typed settings, fallback defaults,
/// and normalized secrets from Azure Key Vault or local SQLite database.
/// </summary>
public sealed class DietDostConfiguration : IDietDostConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly IAppEnvironment? _appEnv;

    public const string DefaultDevJwtKey = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";
    public const string DefaultSuperAdminEmail = "superadmin@dietdost.app";

    public DietDostConfiguration(IConfiguration configuration, IAppEnvironment? appEnv = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnv = appEnv;
    }

    public string JwtKey =>
        _configuration["Jwt:Key"]
        ?? _configuration["JWT_KEY"]
        ?? _configuration["Jwt__Key"]
        ?? (_appEnv?.IsDevelopment == true || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
            ? DefaultDevJwtKey
            : throw new InvalidOperationException("CRITICAL CONFIGURATION ERROR: 'Jwt:Key' is not configured."));

    public string JwtIssuer =>
        _configuration["Jwt:Issuer"] ?? "DietDostGateway";

    public string JwtAudience =>
        _configuration["Jwt:Audience"] ?? "DietDostClient";

    public int JwtExpiryMinutes =>
        int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) && exp > 0 ? exp : 1440;

    public string SuperAdminEmail =>
        _configuration["Auth:SuperAdminEmail"]
        ?? _configuration["SuperAdminEmail"]
        ?? Environment.GetEnvironmentVariable("SUPER_ADMIN_EMAIL")
        ?? DefaultSuperAdminEmail;

    public bool RequireMobileVerification =>
        _configuration.GetValue<bool>("Auth:RequireMobileVerification", false)
        || _configuration.GetValue<bool>("RequireMobileVerification", false);

    public bool AllowRegistration =>
        _configuration.GetValue<bool>("Auth:AllowRegistration", true);

    public string DefaultConnection =>
        _configuration.GetConnectionString("DefaultConnection")
        ?? _configuration["ConnectionStrings:DefaultConnection"]
        ?? "Data Source=diettracker.db";

    public string DatabaseProvider =>
        _configuration["Database:Provider"] ?? "Sqlite";

    public string AiProvider =>
        _configuration["AI:Provider"] ?? "GoogleAI";

    public string GoogleAiModelId =>
        _configuration["AI:GoogleAI:ModelId"] ?? _configuration["AI:ModelId"] ?? "gemini-3-flash-preview";

    public string GoogleAiFallbackModelId =>
        _configuration["AI:GoogleAI:FallbackModelId"] ?? _configuration["AI:FallbackModelId"] ?? "gemini-3.6-flash";

    public string? GoogleAiEndpoint =>
        _configuration["AI:GoogleAI:Endpoint"] ?? _configuration["AI:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/models";

    public string? GoogleAiApiKey =>
        _configuration["AI:GoogleAI:ApiKey"]
        ?? _configuration["AI:ApiKey"]
        ?? _configuration["Gemini:ApiKey"]
        ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    public string AzureOpenAiDeploymentName =>
        _configuration["AI:AzureOpenAI:DeploymentName"] ?? "gpt-5.6-luna";

    public string? AzureOpenAiEndpoint =>
        _configuration["AI:AzureOpenAI:Endpoint"]
        ?? "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1";

    public string? AzureOpenAiApiKey =>
        _configuration["AI:AzureOpenAI:ApiKey"]
        ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

    public int AiMaxTokens =>
        int.TryParse(_configuration["AI:MaxTokens"], out var mt) && mt > 0 ? mt : 8192;

    public double AiTemperature =>
        double.TryParse(_configuration["AI:Temperature"], out var temp) ? temp : 0.2;

    public bool AiShowModelDetails =>
        !bool.TryParse(_configuration["AI:ShowModelDetails"], out var show) || show;

    public string? this[string key] => _configuration[key];
}

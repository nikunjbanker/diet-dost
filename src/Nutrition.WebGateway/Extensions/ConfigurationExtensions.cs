/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nutrition.Infrastructure.Configuration;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Centralized Application Configuration & Secrets Provider for Diet-Dost.
/// Harmonizes configuration sources based on hosting environment:
/// - Deployed Environment (!env.IsDevelopment()):
///   Strictly connects to Azure Key Vault using Passwordless DefaultAzureCredential (User-Assigned Managed Identity).
///   Enforces fail-fast validation for mandatory cryptographic signing keys (Jwt:Key) and secrets.
/// - Local Development (env.IsDevelopment()):
///   Merges configuration from appsettings.Development.json, local database table (AppSecrets),
///   and safe developer defaults.
/// Ensures all consumers injecting IConfiguration receive unified, validated, and normalized configuration
/// without requiring distributed, ad-hoc fallback logic.
/// </summary>
public static class ConfigurationExtensions
{
    public const string DefaultDevJwtKey = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";
    public const string DefaultSuperAdminEmail = "superadmin@dietdost.app";

    public static IConfigurationBuilder AddDietDostAppConfiguration(
        this IConfigurationBuilder builder,
        IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(env);

        var initialConfig = builder is IConfiguration cfgInitial ? cfgInitial : builder.Build();

        if (env.IsDevelopment())
        {
            // 1. Connect local SQLite database secrets table
            var dbConnectionString = initialConfig.GetConnectionString("DefaultConnection") 
                ?? initialConfig["ConnectionStrings:DefaultConnection"]
                ?? "Data Source=diettracker.db";
            builder.AddDatabaseSecrets(dbConnectionString);
        }
        else
        {
            // Deployed Environment (!env.IsDevelopment()):
            // Connect to Azure Key Vault via DefaultAzureCredential (User-Assigned Managed Identity)
            var keyVaultUri = initialConfig["KeyVault:VaultUri"]
                ?? initialConfig["KEY_VAULT_URI"]
                ?? initialConfig.GetConnectionString("dietdost-kv");

            if (!string.IsNullOrWhiteSpace(keyVaultUri))
            {
                builder.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
            }
        }

        // Post-provider normalization and fallback resolution
        var currentConfig = builder is IConfiguration cfgCurrent ? cfgCurrent : builder.Build();
        var normalizedMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // 1. JWT Configuration Normalization
        var resolvedJwtKey = currentConfig["Jwt:Key"]
            ?? currentConfig["Jwt__Key"]
            ?? currentConfig["JWT_KEY"]
            ?? (env.IsDevelopment() ? DefaultDevJwtKey : null);

        if (!string.IsNullOrWhiteSpace(resolvedJwtKey))
        {
            normalizedMap["Jwt:Key"] = resolvedJwtKey;
            normalizedMap["Jwt__Key"] = resolvedJwtKey;
        }

        normalizedMap["Jwt:Issuer"] = currentConfig["Jwt:Issuer"] ?? "DietDostGateway";
        normalizedMap["Jwt:Audience"] = currentConfig["Jwt:Audience"] ?? "DietDostClient";
        normalizedMap["Jwt:ExpiryMinutes"] = currentConfig["Jwt:ExpiryMinutes"] ?? "1440";

        // 2. Auth & Identity Normalization (SuperAdmin & Mobile Verification)
        var resolvedSuperAdminEmail = currentConfig["Auth:SuperAdminEmail"]
            ?? currentConfig["SuperAdminEmail"]
            ?? currentConfig["SUPER_ADMIN_EMAIL"]
            ?? DefaultSuperAdminEmail;

        normalizedMap["Auth:SuperAdminEmail"] = resolvedSuperAdminEmail;
        normalizedMap["SuperAdminEmail"] = resolvedSuperAdminEmail;

        var resolvedRequireMobileVerification = currentConfig["Auth:RequireMobileVerification"]
            ?? currentConfig["RequireMobileVerification"]
            ?? currentConfig["REQUIRE_MOBILE_VERIFICATION"]
            ?? "false";

        normalizedMap["Auth:RequireMobileVerification"] = resolvedRequireMobileVerification;
        normalizedMap["RequireMobileVerification"] = resolvedRequireMobileVerification;

        normalizedMap["Auth:AllowRegistration"] = currentConfig["Auth:AllowRegistration"] ?? "true";

        // 3. Database Normalization
        normalizedMap["ConnectionStrings:DefaultConnection"] = currentConfig.GetConnectionString("DefaultConnection")
            ?? currentConfig["ConnectionStrings:DefaultConnection"]
            ?? (env.IsDevelopment() ? "Data Source=diettracker.db" : "Data Source=/app/data/diet_dost.db;Cache=Shared");

        normalizedMap["Database:Provider"] = currentConfig["Database:Provider"] ?? "Sqlite";

        // 4. AI Provider Normalization
        normalizedMap["AI:Provider"] = currentConfig["AI:Provider"] ?? "GoogleAI";
        normalizedMap["AI:GoogleAI:ModelId"] = currentConfig["AI:GoogleAI:ModelId"] ?? currentConfig["AI:ModelId"] ?? "gemini-3-flash-preview";
        normalizedMap["AI:GoogleAI:FallbackModelId"] = currentConfig["AI:GoogleAI:FallbackModelId"] ?? currentConfig["AI:FallbackModelId"] ?? "gemini-3.6-flash";
        normalizedMap["AI:GoogleAI:Endpoint"] = currentConfig["AI:GoogleAI:Endpoint"] ?? currentConfig["AI:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/models";

        var resolvedGoogleAiApiKey = currentConfig["AI:GoogleAI:ApiKey"]
            ?? currentConfig["AI:ApiKey"]
            ?? currentConfig["Gemini:ApiKey"]
            ?? currentConfig["GEMINI_API_KEY"];

        if (!string.IsNullOrWhiteSpace(resolvedGoogleAiApiKey))
        {
            normalizedMap["AI:GoogleAI:ApiKey"] = resolvedGoogleAiApiKey;
            normalizedMap["AI:ApiKey"] = resolvedGoogleAiApiKey;
        }

        normalizedMap["AI:AzureOpenAI:DeploymentName"] = currentConfig["AI:AzureOpenAI:DeploymentName"] ?? "gpt-5.6-luna";
        normalizedMap["AI:AzureOpenAI:Endpoint"] = currentConfig["AI:AzureOpenAI:Endpoint"] ?? "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1";

        var resolvedAzureApiKey = currentConfig["AI:AzureOpenAI:ApiKey"] ?? currentConfig["AZURE_OPENAI_API_KEY"];
        if (!string.IsNullOrWhiteSpace(resolvedAzureApiKey))
        {
            normalizedMap["AI:AzureOpenAI:ApiKey"] = resolvedAzureApiKey;
        }

        normalizedMap["AI:MaxTokens"] = currentConfig["AI:MaxTokens"] ?? "8192";
        normalizedMap["AI:Temperature"] = currentConfig["AI:Temperature"] ?? "0.2";
        normalizedMap["AI:ShowModelDetails"] = currentConfig["AI:ShowModelDetails"] ?? "true";

        if (normalizedMap.Count > 0)
        {
            builder.AddInMemoryCollection(normalizedMap);
        }

        return builder;
    }

    /// <summary>
    /// Validates mandatory configuration secrets in non-development environments.
    /// Fails fast during application startup if critical cryptographic keys are missing.
    /// </summary>
    public static void ValidateRequiredDeployedSecrets(this IConfiguration configuration, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(env);

        if (!env.IsDevelopment())
        {
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: 'Jwt:Key' is not configured. " +
                    "In non-development / deployed environments, the cryptographic JWT signing key MUST be provided via Azure Key Vault or secure environment variables.");
            }

            if (System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY CONFIGURATION ERROR: 'Jwt:Key' must be at least 32 bytes (256 bits) for HMAC-SHA256 signing.");
            }
        }
    }
}

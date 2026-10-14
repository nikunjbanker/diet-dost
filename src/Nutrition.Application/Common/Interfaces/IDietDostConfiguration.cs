/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Centralized application configuration contract for Diet-Dost.
/// Exposes strongly-typed access to merged application settings and secrets across environments
/// (Azure Key Vault in deployed ACA, SQLite database / appsettings.Development.json in local dev).
/// </summary>
public interface IDietDostConfiguration
{
    // JWT Secrets & Configuration
    string JwtKey { get; }
    string JwtIssuer { get; }
    string JwtAudience { get; }
    int JwtExpiryMinutes { get; }

    // Auth & Identity
    string SuperAdminEmail { get; }
    bool RequireMobileVerification { get; }
    bool AllowRegistration { get; }

    // Database
    string DefaultConnection { get; }
    string DatabaseProvider { get; }

    // AI Providers
    string AiProvider { get; }
    string GoogleAiModelId { get; }
    string GoogleAiFallbackModelId { get; }
    string? GoogleAiEndpoint { get; }
    string? GoogleAiApiKey { get; }

    string AzureOpenAiDeploymentName { get; }
    string? AzureOpenAiEndpoint { get; }
    string? AzureOpenAiApiKey { get; }

    int AiMaxTokens { get; }
    double AiTemperature { get; }
    bool AiShowModelDetails { get; }

    // Direct configuration indexer
    string? this[string key] { get; }
}

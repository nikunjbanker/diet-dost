/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Azure.Identity;
using Nutrition.Infrastructure.Configuration;
using Nutrition.WebGateway.Extensions;

// ====================================================================================
// Diet-Dost Web Gateway — Program Entrypoint
// Architecture: Clean Architecture, Domain-Driven Design (DDD) & OWASP ASVS Governance
// Target: .NET 11 RC Standalone Aspire Host
// ====================================================================================

var builder = WebApplication.CreateBuilder(args);

// 0a. Load Application Secrets from Database Table into IConfiguration Hierarchy (Local Fallback)
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";
builder.Configuration.AddDatabaseSecrets(dbConnectionString);

// 0b. Azure Key Vault Secrets Integration (Deployed / Cloud Environment)
// In deployed environments (non-development or when KeyVault:VaultUri is configured),
// retrieve application secrets (Jwt:Key, Auth:SuperAdminEmail, Auth:RequireMobileVerification,
// AI:GoogleAI:ApiKey, AI:AzureOpenAI:ApiKey, ConnectionStrings, etc.) directly from Azure Key Vault.
// Azure Key Vault configuration provider takes highest precedence, overriding database defaults and appsettings.
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"]
    ?? builder.Configuration["KEY_VAULT_URI"]
    ?? builder.Configuration.GetConnectionString("dietdost-kv");

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

// 1. Core Framework & Distributed Telemetry (Aspire / OpenTelemetry)
builder.Services.AddAppTelemetry(builder.Logging, builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Application Domain Services & Storage Infrastructure
builder.Services.AddAppServices(builder.Configuration);

// 3. Security, Authentication & Rate Limiting (OWASP ASVS & A04)
builder.Services.AddAppSecurityAndAuth(builder.Configuration, builder.Environment);
builder.Services.AddAppCors(builder.Environment);
builder.Services.AddAppRateLimiting(builder.Environment);

var app = builder.Build();

// 4. Database Schema Verification, PRAGMA Migration & Demo Seeding
await app.InitializeAndSeedDatabaseAsync(builder.Configuration);

// 5. HTTP Middleware Pipeline
app.UseAppMiddlewarePipeline();

app.Run();

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

// 0. Centralized Diet-Dost Application & Secrets Configuration
// In deployed environments (!env.IsDevelopment()): secrets are retrieved from Azure Key Vault using DefaultAzureCredential.
// In local development (env.IsDevelopment()): secrets are merged from appsettings.Development.json, local database, and dev defaults.
// Wherever IConfiguration is injected, all merged secrets and settings are available without distributed checks.
builder.Configuration.AddDietDostAppConfiguration(builder.Environment);
builder.Configuration.ValidateRequiredDeployedSecrets(builder.Environment);

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

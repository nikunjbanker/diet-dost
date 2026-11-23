/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application;
using Nutrition.Application.Agents;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Services;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Configuration;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.Mcp;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Registers domain, application, and infrastructure services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string[] AdditionalJsonMimeTypes = ["application/json", "text/json"];

    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Strongly-Typed Options Pattern with FluentValidation (JwtOptions, AuthOptions, AiOptions, DatabaseOptions)
        services.AddDietDostOptions(configuration);

        // Centralized Strongly-Typed Configuration & Secrets Accessor
        services.AddSingleton<IDietDostConfiguration, DietDostConfiguration>();

        // Storage Infrastructure (Swappable SQLite V1 per SDD section 3.1)
        services.AddStorageInfrastructure(configuration);

        // Security Infrastructure (Password hashing, JWT generation, OTP verification)
        services.AddSecurityInfrastructure();

        // Clinical Dietetics Domain Service
        services.AddScoped<ClinicalDietitianService>();

        // AI Agent Infrastructure (Microsoft Agent Framework + Google AI Gemini)
        services.AddHttpClient<MicrosoftAgentFoodVisionService>(client =>
        {
            // Vision analysis with large images over Gemini can take 30-90s.
            // We allow 120s total so all model fallbacks can complete before timeout.
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IFoodVisionAgent, MicrosoftAgentFoodVisionService>();
        services.AddHostedService<Nutrition.Infrastructure.Services.QrSessionCleanupService>();

        // Native .NET 11 Clean Architecture Application Services & CQRS Dispatcher
        services.AddApplicationServices();

        // Database Schema Initializer Strategy, Factory & Ordered Data Seeders (OOP / SOLID Pattern)
        services.AddScoped<Nutrition.WebGateway.Initialization.Abstractions.IDatabaseInitializerFactory, Nutrition.WebGateway.Initialization.Factories.DatabaseInitializerFactory>();
        services.AddScoped<Nutrition.WebGateway.Initialization.Abstractions.IDatabaseSeeder, Nutrition.WebGateway.Initialization.Seeders.AppSecretsSeeder>();
        services.AddScoped<Nutrition.WebGateway.Initialization.Abstractions.IDatabaseSeeder, Nutrition.WebGateway.Initialization.Seeders.DefaultDataSeeder>();
        services.AddScoped<Nutrition.WebGateway.Initialization.Abstractions.IDatabaseSeeder, Nutrition.WebGateway.Initialization.Seeders.StorageMediaMirrorSeeder>();
        services.AddScoped<Nutrition.WebGateway.Initialization.DatabaseInitializationService>();

        // Response Compression for Mobile payloads (Brotli & Gzip)
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(AdditionalJsonMimeTypes);
        });
        services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });
        services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        // Ambient User Claims Context Provider (OWASP ASVS tenant isolation)
        services.AddHttpContextAccessor();
        services.AddScoped<Nutrition.Application.Common.Interfaces.ICurrentUserService, Services.CurrentUserService>();
        services.AddSignalR();
        services.AddScoped<IInAppNotificationService, Services.InAppNotificationService>();

        // Active User Monitoring & Azure Monitor Metrics Services
        services.AddSingleton<IUserPresenceTracker>(sp => new Services.UserPresenceTracker(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<Services.UserPresenceTracker>>()));
        services.AddHostedService<Services.AzureMonitorPresenceMetricsWorker>();

        // Native ASP.NET Core Health Checks (/healthz liveness & /ready readiness)
        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("WebGateway service is live."), tags: ["live"])
            .AddCheck<Health.DatabaseHealthCheck>("database", tags: ["ready"]);

        // Data Protection key persistence across container replicas & restarts on mounted Azure Files share
        var dpBuilder = services.AddDataProtection();
        var keysDirectory = "/app/data/keys";
        try
        {
            if (Directory.Exists("/app/data"))
            {
                if (!Directory.Exists(keysDirectory))
                {
                    Directory.CreateDirectory(keysDirectory);
                }
                Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions.PersistKeysToFileSystem(dpBuilder, new DirectoryInfo(keysDirectory));
            }
        }
        catch
        {
            // Non-fatal fallback to default ephemeral storage
        }

        // Official Model Context Protocol (MCP) Server with HTTP/SSE Transport
        services.AddDietDostMcpServer();

        return services;
    }
}


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
using Nutrition.Application.Services;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Configuration;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Registers domain, application, and infrastructure services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        // Native .NET 11 Clean Architecture Application Services & CQRS Dispatcher
        services.AddApplicationServices();

        // Response Compression for Mobile payloads (Brotli & Gzip)
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "text/json"
            });
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

        return services;
    }
}

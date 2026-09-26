/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Nutrition.Application.Common;
using Nutrition.Domain.Clinical;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Configures OpenTelemetry logging, metrics, tracing, and OTLP exporters for the WebGateway.
/// Integrates seamlessly with the Aspire Developer Dashboard.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAppTelemetry(
        this IServiceCollection services,
        ILoggingBuilder logging,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configuration);

        // Configure formatted OpenTelemetry logging with scopes
        logging.AddOpenTelemetry(otLogging =>
        {
            otLogging.IncludeFormattedMessage = true;
            otLogging.IncludeScopes = true;
        });

        // Configure metrics and distributed tracing
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(NutritionTelemetry.ServiceName)
                       .AddAspNetCoreInstrumentation(options =>
                       {
                           options.RecordException = true;
                       })
                       .AddHttpClientInstrumentation(options =>
                       {
                           options.RecordException = true;
                       });
            });

        // Export to Aspire OTLP collector if endpoint is supplied
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }

        return services;
    }
}

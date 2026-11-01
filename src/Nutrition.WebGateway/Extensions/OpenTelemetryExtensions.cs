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
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
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
                       .AddRuntimeInstrumentation()
                       .AddMeter(NutritionTelemetry.ServiceName);
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
        if (!string.IsNullOrWhiteSpace(configuration.GetValue<string>("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Integrate Azure Monitor / Application Insights (Logs Only with Entra ID / Managed Identity auth)
        var appInsightsConnectionString = configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? configuration.GetValue<string>("ApplicationInsights:ConnectionString");

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            var clientId = configuration.GetValue<string>("AZURE_CLIENT_ID");
            var credential = !string.IsNullOrWhiteSpace(clientId)
                ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId })
                : new DefaultAzureCredential();

            logging.AddOpenTelemetry(otLogging =>
            {
                otLogging.AddAzureMonitorLogExporter(options =>
                {
                    options.ConnectionString = appInsightsConnectionString;
                    options.Credential = credential;
                });
            });
        }

        return services;
    }
}


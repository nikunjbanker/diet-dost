/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dotnet;
using Microsoft.Extensions.Configuration;
using Nutrition.AppHost.Configuration;

namespace Nutrition.AppHost.Extensions;

/// <summary>
/// Extension methods for registering and configuring the WebGateway project resource within Aspire AppHost.
/// Supports Project Resource v2 (DotnetProjectResource) and dual-database persistence parameterization (SQLite & Azure SQL).
/// Follows Single Responsibility and Open/Closed principles.
/// </summary>
public static class WebGatewayResourceExtensions
{
    private const int GatewayPort = 5240;
    private const string ResourceName = "web-gateway";

    /// <summary>
    /// Adds and configures the Nutrition.WebGateway project with its HTTP endpoints,
    /// persistence settings (SQLite or Azure SQL), AI provider environment variables, optional database resource, and optional Azure Key Vault reference.
    /// </summary>
    public static IResourceBuilder<DotnetProjectResource> AddWebGateway(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<AzureKeyVaultResource>? keyVault = null,
        IResourceBuilder<IResourceWithConnectionString>? database = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var aiOptions = AppHostAiOptions.FromConfiguration(builder.Configuration);

        // Resolve Dual Database Provider (Sqlite vs AzureSql/SqlServer)
        var dbProvider = builder.Configuration["Database:Provider"]
            ?? builder.Configuration["Database__Provider"]
            ?? "Sqlite";

        var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=diettracker.db";

        var azureSqlConn = builder.Configuration.GetConnectionString("AzureSqlConnection")
            ?? builder.Configuration["ConnectionStrings:AzureSqlConnection"];

        var webGateway = builder.AddDotnetProject(ResourceName, "../Nutrition.WebGateway/Nutrition.WebGateway.csproj")
            .WithHttpEndpoint(port: GatewayPort, isProxied: false)
            .WithExternalHttpEndpoints()
            .WithEnvironment("Database__Provider", dbProvider)
            .WithEnvironment("ConnectionStrings__DefaultConnection", defaultConn)
            .WithEnvironment("AI__Provider", aiOptions.Provider)
            .WithEnvironment("AI__GoogleAI__ApiKey", aiOptions.GeminiApiKey ?? string.Empty)
            .WithEnvironment("AI__ApiKey", aiOptions.GeminiApiKey ?? string.Empty)
            .WithEnvironment("AI__GoogleAI__ModelId", aiOptions.GeminiModelId)
            .WithEnvironment("AI__GoogleAI__FallbackModelId", aiOptions.GeminiFallbackModelId);

        // Forward Azure SQL connection string if configured
        if (!string.IsNullOrWhiteSpace(azureSqlConn))
        {
            webGateway.WithEnvironment("ConnectionStrings__AzureSqlConnection", azureSqlConn);
        }

        if (database != null)
        {
            webGateway.WithReference(database);
        }

        if (keyVault != null && (builder.ExecutionContext.IsPublishMode || !string.IsNullOrWhiteSpace(builder.Configuration["Azure:SubscriptionId"])))
        {
            webGateway.WithReference(keyVault);
        }
        else
        {
            // In local run mode without Azure subscription provisioning, forward any explicitly configured Key Vault URI
            var kvUri = builder.Configuration["KeyVault:VaultUri"]
                ?? builder.Configuration["KEY_VAULT_URI"]
                ?? builder.Configuration.GetConnectionString("dietdost-kv");

            if (!string.IsNullOrWhiteSpace(kvUri))
            {
                webGateway.WithEnvironment("KeyVault__VaultUri", kvUri);
            }
        }

        if (!string.IsNullOrWhiteSpace(aiOptions.AzureApiKey))
        {
            webGateway.WithEnvironment("AI__AzureOpenAI__ApiKey", aiOptions.AzureApiKey);
        }

        if (!string.IsNullOrWhiteSpace(aiOptions.AzureEndpoint))
        {
            webGateway.WithEnvironment("AI__AzureOpenAI__Endpoint", aiOptions.AzureEndpoint);
        }

        if (!string.IsNullOrWhiteSpace(aiOptions.AzureDeploymentName))
        {
            webGateway.WithEnvironment("AI__AzureOpenAI__DeploymentName", aiOptions.AzureDeploymentName);
        }

        // Configure Azure Container Apps publication settings
        webGateway.PublishAsAzureContainerApp((infrastructure, containerApp) =>
        {
            containerApp.Template.Scale.MinReplicas = 1;
            containerApp.Template.Scale.MaxReplicas = 1;
        });

        return webGateway;
    }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Nutrition.AppHost.Configuration;

namespace Nutrition.AppHost.Extensions;

/// <summary>
/// Extension methods for registering and configuring the WebGateway project resource within Aspire AppHost.
/// Follows Single Responsibility and Open/Closed principles.
/// </summary>
public static class WebGatewayResourceExtensions
{
    private const int GatewayPort = 5240;
    private const string ResourceName = "web-gateway";

    /// <summary>
    /// Adds and configures the Nutrition.WebGateway project with its HTTP endpoints,
    /// persistence settings, and AI provider environment variables.
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddWebGateway(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var aiOptions = AppHostAiOptions.FromConfiguration(builder.Configuration);

        var webGateway = builder.AddProject<Projects.Nutrition_WebGateway>(ResourceName)
            .WithHttpEndpoint(port: GatewayPort, isProxied: false)
            .WithExternalHttpEndpoints()
            .WithEnvironment("Database__Provider", "Sqlite")
            .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=diettracker.db")
            .WithEnvironment("AI__Provider", aiOptions.Provider)
            .WithEnvironment("AI__GoogleAI__ApiKey", aiOptions.GeminiApiKey ?? string.Empty)
            .WithEnvironment("AI__ApiKey", aiOptions.GeminiApiKey ?? string.Empty)
            .WithEnvironment("AI__GoogleAI__ModelId", aiOptions.GeminiModelId)
            .WithEnvironment("AI__GoogleAI__FallbackModelId", aiOptions.GeminiFallbackModelId);

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

        return webGateway;
    }
}

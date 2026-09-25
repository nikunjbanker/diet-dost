using Microsoft.Extensions.Configuration;

namespace Nutrition.AppHost.Configuration;

/// <summary>
/// Strongly-typed configuration options for AI provider credentials and model identifiers.
/// Encapsulates environment variable fallbacks and provider selection.
/// </summary>
public sealed record AppHostAiOptions(
    string Provider,
    string? GeminiApiKey,
    string GeminiModelId,
    string GeminiFallbackModelId,
    string? AzureApiKey,
    string? AzureEndpoint,
    string AzureDeploymentName)
{
    public static AppHostAiOptions FromConfiguration(IConfiguration configuration)
    {
        var geminiKey = configuration["AI:GoogleAI:ApiKey"]
            ?? configuration["AI:ApiKey"]
            ?? configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("AI__GoogleAI__ApiKey")
            ?? Environment.GetEnvironmentVariable("AI__ApiKey")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        var aiProvider = configuration["AI:Provider"]
            ?? Environment.GetEnvironmentVariable("AI__Provider")
            ?? "GoogleAI";

        var azureKey = configuration["AI:AzureOpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey");

        var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]
            ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__Endpoint");

        var azureDeployment = configuration["AI:AzureOpenAI:DeploymentName"]
            ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__DeploymentName")
            ?? "gpt-5.6-luna";

        var geminiModelId = configuration["AI:GoogleAI:ModelId"]
            ?? configuration["AI:ModelId"]
            ?? "gemini-3-flash-preview";

        var geminiFallbackModelId = configuration["AI:GoogleAI:FallbackModelId"]
            ?? configuration["AI:FallbackModelId"]
            ?? "gemini-3.6-flash";

        return new AppHostAiOptions(
            Provider: aiProvider,
            GeminiApiKey: geminiKey,
            GeminiModelId: geminiModelId,
            GeminiFallbackModelId: geminiFallbackModelId,
            AzureApiKey: azureKey,
            AzureEndpoint: azureEndpoint,
            AzureDeploymentName: azureDeployment);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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
    private sealed class RawAiSection
    {
        public string? Provider { get; set; }
        public RawGoogleAiSection? GoogleAI { get; set; }
        public RawAzureOpenAiSection? AzureOpenAI { get; set; }
    }

    private sealed class RawGoogleAiSection
    {
        public string? ModelId { get; set; }
        public string? FallbackModelId { get; set; }
        public string? ApiKey { get; set; }
    }

    private sealed class RawAzureOpenAiSection
    {
        public string? DeploymentName { get; set; }
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
    }

    public static AppHostAiOptions FromConfiguration(IConfiguration configuration)
    {
        var aiOptions = configuration.GetSection("AI").Get<RawAiSection>() ?? new RawAiSection();

        var geminiKey = ResolveUsableKey(
            aiOptions.GoogleAI?.ApiKey,
            Environment.GetEnvironmentVariable("AI__GoogleAI__ApiKey"),
            Environment.GetEnvironmentVariable("AI__ApiKey"),
            Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_AI_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));

        var aiProvider = !string.IsNullOrWhiteSpace(aiOptions.Provider)
            ? aiOptions.Provider
            : Environment.GetEnvironmentVariable("AI__Provider") ?? "GoogleAI";

        var azureKey = ResolveUsableKey(
            aiOptions.AzureOpenAI?.ApiKey,
            Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey"));

        var azureEndpoint = !string.IsNullOrWhiteSpace(aiOptions.AzureOpenAI?.Endpoint)
            ? aiOptions.AzureOpenAI.Endpoint
            : Environment.GetEnvironmentVariable("AI__AzureOpenAI__Endpoint");

        var azureDeployment = !string.IsNullOrWhiteSpace(aiOptions.AzureOpenAI?.DeploymentName)
            ? aiOptions.AzureOpenAI.DeploymentName
            : Environment.GetEnvironmentVariable("AI__AzureOpenAI__DeploymentName") ?? "gpt-5.6-luna";

        var geminiModelId = !string.IsNullOrWhiteSpace(aiOptions.GoogleAI?.ModelId)
            ? aiOptions.GoogleAI.ModelId
            : "gemini-3-flash-preview";

        var geminiFallbackModelId = !string.IsNullOrWhiteSpace(aiOptions.GoogleAI?.FallbackModelId)
            ? aiOptions.GoogleAI.FallbackModelId
            : "gemini-3.6-flash";

        return new AppHostAiOptions(
            Provider: aiProvider,
            GeminiApiKey: geminiKey,
            GeminiModelId: geminiModelId,
            GeminiFallbackModelId: geminiFallbackModelId,
            AzureApiKey: azureKey,
            AzureEndpoint: azureEndpoint,
            AzureDeploymentName: azureDeployment);
    }

    private static string? ResolveUsableKey(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c))
            {
                var trimmed = c.Trim();
                if (trimmed.Length >= 20 &&
                    !trimmed.Contains('*') &&
                    !trimmed.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.Contains('<'))
                {
                    return trimmed;
                }
            }
        }
        return null;
    }
}

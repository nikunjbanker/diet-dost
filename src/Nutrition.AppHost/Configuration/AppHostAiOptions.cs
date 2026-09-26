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
    public static AppHostAiOptions FromConfiguration(IConfiguration configuration)
    {
        var geminiKey = ResolveUsableKey(
            configuration["AI:GoogleAI:ApiKey"],
            configuration["AI:ApiKey"],
            configuration["Gemini:ApiKey"],
            configuration["GoogleAI:ApiKey"],
            Environment.GetEnvironmentVariable("AI__GoogleAI__ApiKey"),
            Environment.GetEnvironmentVariable("AI__ApiKey"),
            Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_AI_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));

        var aiProvider = configuration["AI:Provider"]
            ?? Environment.GetEnvironmentVariable("AI__Provider")
            ?? "GoogleAI";

        var azureKey = ResolveUsableKey(
            configuration["AI:AzureOpenAI:ApiKey"],
            Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey"));

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

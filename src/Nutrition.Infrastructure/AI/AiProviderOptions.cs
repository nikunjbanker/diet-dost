/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;

namespace Nutrition.Infrastructure.AI;

public sealed class AiProviderOptions
{
    public string Provider { get; init; } = "GoogleAI";
    public string ModelId { get; init; } = "gemini-3-flash-preview";
    public string? FallbackModelId { get; init; } = "gemini-3.6-flash";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? AzureApiKey { get; init; }
    public string? AzureEndpoint { get; init; }
    public string? AzureDeploymentName { get; init; }
    public int MaxTokens { get; init; } = 8192;
    public double Temperature { get; init; } = 0.2;
    public bool ShowModelDetails { get; init; } = true;

    public static AiProviderOptions FromConfiguration(IConfiguration config)
    {
        return new AiProviderOptions
        {
            Provider = config["AI:Provider"] ?? "GoogleAI",
            ModelId = config["AI:GoogleAI:ModelId"] ?? config["AI:ModelId"] ?? "gemini-3-flash-preview",
            FallbackModelId = config["AI:GoogleAI:FallbackModelId"] ?? config["AI:FallbackModelId"] ?? "gemini-3.6-flash",
            ApiKey = config["AI:GoogleAI:ApiKey"] ?? config["AI:ApiKey"] ?? config["Gemini:ApiKey"] ?? config["GoogleAI:ApiKey"] ?? Environment.GetEnvironmentVariable("AI__GoogleAI__ApiKey") ?? Environment.GetEnvironmentVariable("AI__ApiKey") ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Endpoint = config["AI:GoogleAI:Endpoint"] ?? config["AI:Endpoint"],
            AzureApiKey = config["AI:AzureOpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey"),
            AzureEndpoint = config["AI:AzureOpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__Endpoint"),
            AzureDeploymentName = config["AI:AzureOpenAI:DeploymentName"] ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__DeploymentName") ?? "gpt-5.6-luna",
            MaxTokens = int.TryParse(config["AI:MaxTokens"], out var maxTokens) && maxTokens > 0 ? maxTokens : 8192,
            Temperature = double.TryParse(config["AI:Temperature"], out var temperature) ? temperature : 0.2,
            ShowModelDetails = !bool.TryParse(config["AI:ShowModelDetails"], out var showModelDetails) || showModelDetails
        };
    }

    public IReadOnlyList<string> GetModels()
    {
        if (string.Equals(Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { AzureDeploymentName ?? "gpt-5.6-luna" };
        }

        var models = new List<string>();
        if (!string.IsNullOrWhiteSpace(ModelId))
            models.Add(ModelId);
        if (!string.IsNullOrWhiteSpace(FallbackModelId) && !models.Contains(FallbackModelId, StringComparer.OrdinalIgnoreCase))
            models.Add(FallbackModelId);

        foreach (var model in new[] { "gemini-3-flash-preview", "gemini-3.7-flash", "gemini-3.6-flash" })
        {
            if (!models.Contains(model, StringComparer.OrdinalIgnoreCase))
                models.Add(model);
        }

        return models;
    }
}

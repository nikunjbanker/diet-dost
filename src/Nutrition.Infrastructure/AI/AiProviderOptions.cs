/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Nutrition.Application.Common.Options;

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

    public static AiProviderOptions FromAiOptions(AiOptions aiOptions)
    {
        ArgumentNullException.ThrowIfNull(aiOptions);
        return new AiProviderOptions
        {
            Provider = !string.IsNullOrWhiteSpace(aiOptions.Provider) ? aiOptions.Provider : "GoogleAI",
            ModelId = !string.IsNullOrWhiteSpace(aiOptions.GoogleAI?.ModelId) ? aiOptions.GoogleAI.ModelId : "gemini-3-flash-preview",
            FallbackModelId = !string.IsNullOrWhiteSpace(aiOptions.GoogleAI?.FallbackModelId) ? aiOptions.GoogleAI.FallbackModelId : "gemini-3.6-flash",
            ApiKey = aiOptions.GoogleAI?.ApiKey,
            Endpoint = aiOptions.GoogleAI?.Endpoint,
            AzureApiKey = aiOptions.AzureOpenAI?.ApiKey,
            AzureEndpoint = aiOptions.AzureOpenAI?.Endpoint,
            AzureDeploymentName = !string.IsNullOrWhiteSpace(aiOptions.AzureOpenAI?.DeploymentName) ? aiOptions.AzureOpenAI.DeploymentName : "gpt-5.6-luna",
            MaxTokens = aiOptions.MaxTokens > 0 ? aiOptions.MaxTokens : 8192,
            Temperature = aiOptions.Temperature,
            ShowModelDetails = aiOptions.ShowModelDetails
        };
    }

    public static AiProviderOptions FromConfiguration(IConfiguration config)
    {
        var aiOptions = config.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        return FromAiOptions(aiOptions);
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

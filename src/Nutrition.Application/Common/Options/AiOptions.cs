/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Options;

/// <summary>
/// Strongly-typed Options for AI Nutrition Vision and LLM Models.
/// Bound to configuration section "AI".
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "GoogleAI";
    public GoogleAiModelOptions GoogleAI { get; set; } = new();
    public AzureOpenAiModelOptions AzureOpenAI { get; set; } = new();
    public int MaxTokens { get; set; } = 8192;
    public double Temperature { get; set; } = 0.2;
    public bool ShowModelDetails { get; set; } = true;
}

public sealed class GoogleAiModelOptions
{
    public string ModelId { get; set; } = "gemini-3-flash-preview";
    public string FallbackModelId { get; set; } = "gemini-3.6-flash";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class AzureOpenAiModelOptions
{
    public string DeploymentName { get; set; } = "gpt-5.6-luna";
    public string Endpoint { get; set; } = "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1";
    public string ApiKey { get; set; } = string.Empty;
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
#pragma warning disable OPENAI001

using OpenAI.Responses;
using System.ClientModel;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

internal sealed class AzureOpenAiProvider : IAiFoodAnalysisProvider
{
    private readonly AiProviderOptions _options;
    private readonly ILogger<AzureOpenAiProvider> _logger;

    public AzureOpenAiProvider(AiProviderOptions options, ILogger<AzureOpenAiProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string ProviderName => "azure_openai";
    public bool SupportsVision => true;

    public Task<IndianMealAnalysisResult?> AnalyzeTextAsync(string prompt, string modelId, CancellationToken ct) =>
        SendAsync(modelId, new[] { ResponseItem.CreateUserMessageItem(prompt) }, ct);

    public Task<IndianMealAnalysisResult?> AnalyzePhotoAsync(
        byte[] imageBytes, string mimeType, string prompt, string modelId, CancellationToken ct)
    {
        var imageDataUrl = $"data:{(string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType)};base64,{Convert.ToBase64String(imageBytes)}";
        var parts = new[]
        {
            ResponseContentPart.CreateInputTextPart(prompt),
            ResponseContentPart.CreateInputImagePart(imageDataUrl, ResponseImageDetailLevel.High)
        };
        return SendAsync(modelId, new[] { ResponseItem.CreateUserMessageItem(parts) }, ct);
    }

    private async Task<IndianMealAnalysisResult?> SendAsync(
        string modelId, IEnumerable<ResponseItem> inputItems, CancellationToken ct)
    {
        var apiKey = (_options.AzureApiKey ?? Environment.GetEnvironmentVariable("AI__AzureOpenAI__ApiKey"))?.Trim();
        var endpoint = _options.AzureEndpoint?.Trim();
        if (!IsUsableApiKey(apiKey) || endpoint is null || endpoint.Length == 0 || endpoint.Contains('<'))
        {
            _logger.LogWarning("Azure OpenAI provider is selected but AI:AzureOpenAI:ApiKey or Endpoint is not configured.");
            return null;
        }

        var client = new ResponsesClient(
            new ApiKeyCredential(apiKey!),
            new ResponsesClientOptions { Endpoint = new Uri(endpoint.TrimEnd('/') + "/") });
        var options = new CreateResponseOptions
        {
            Model = modelId,
            MaxOutputTokenCount = _options.MaxTokens
        };
        foreach (var item in inputItems)
            options.InputItems.Add(item);

        var response = await client.CreateResponseAsync(options, ct);
        return AiJsonParser.Parse(response.Value.GetOutputText(), _logger, modelId);
    }

    private static bool IsUsableApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var value = apiKey.Trim();
        return value.Length >= 20 &&
               !value.Contains('*') &&
               !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains('<');
    }
}

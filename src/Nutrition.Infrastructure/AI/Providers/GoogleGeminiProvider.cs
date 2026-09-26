/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

internal sealed class GoogleGeminiProvider : IAiFoodAnalysisProvider
{
    private readonly AiProviderOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleGeminiProvider> _logger;

    public GoogleGeminiProvider(AiProviderOptions options, HttpClient httpClient, ILogger<GoogleGeminiProvider> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ProviderName => "google_gemini";
    public bool SupportsVision => true;

    public async Task<IndianMealAnalysisResult?> AnalyzePhotoAsync(
        byte[] imageBytes, string mimeType, string prompt, string modelId, CancellationToken ct)
    {
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new { inline_data = new { mime_type = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType, data = Convert.ToBase64String(imageBytes) } }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = _options.Temperature,
                max_output_tokens = _options.MaxTokens
            }
        };

        return await SendAsync(modelId, payload, ct);
    }

    public async Task<IndianMealAnalysisResult?> AnalyzeTextAsync(string prompt, string modelId, CancellationToken ct)
    {
        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = _options.Temperature,
                max_output_tokens = _options.MaxTokens
            }
        };

        return await SendAsync(modelId, payload, ct);
    }

    private async Task<IndianMealAnalysisResult?> SendAsync(string modelId, object payload, CancellationToken ct)
    {
        var apiKey = _options.ApiKey
            ?? Environment.GetEnvironmentVariable("AI__ApiKey")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_AI_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var configuredApiKey = apiKey?.Trim();
        if (!IsUsableApiKey(configuredApiKey))
            return null;

        var endpoint = (_options.Endpoint ?? "https://generativelanguage.googleapis.com/v1beta/models").TrimEnd('/');
        var url = $"{endpoint}/{modelId}:generateContent?key={Uri.EscapeDataString(configuredApiKey!)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        using var response = await _httpClient.SendAsync(request, ct);

        if ((int)response.StatusCode is 429 or 503)
        {
            var retryDelay = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? 5 : 3;
            _logger.LogWarning("Google AI model {Model} returned {Status}; retrying once after {Delay}s.", modelId, response.StatusCode, retryDelay);
            await Task.Delay(TimeSpan.FromSeconds(retryDelay), ct);
            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            using var retryResponse = await _httpClient.SendAsync(retryRequest, ct);
            return await ParseResponseAsync(retryResponse, modelId, ct);
        }

        return await ParseResponseAsync(response, modelId, ct);
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

    private async Task<IndianMealAnalysisResult?> ParseResponseAsync(HttpResponseMessage response, string modelId, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google AI generateContent returned {Status} for model {Model}: {Body}",
                response.StatusCode, modelId, await response.Content.ReadAsStringAsync(ct));
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var text = parts.EnumerateArray()
            .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value.Contains('{'));
        return AiJsonParser.Parse(text, _logger, modelId);
    }
}

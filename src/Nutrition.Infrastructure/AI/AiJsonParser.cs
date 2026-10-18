/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

/// <summary>
/// Resilient structured output parser for LLM responses.
/// Implements high-performance direct schema deserialization with graceful fallbacks
/// for fence-wrapped or partially formatted responses.
/// </summary>
internal static class AiJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IndianMealAnalysisResult? Parse(string? responseText, ILogger logger, string modelId)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var trimmed = responseText.Trim();

        // 1. Fast-Path: Direct Structured Output Deserialization (Zero string manipulation)
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            try
            {
                var directResult = JsonSerializer.Deserialize<IndianMealAnalysisResult>(trimmed, JsonOptions);
                if (directResult != null && IsValidMealResult(directResult))
                {
                    EnforceDeterministicTotals(directResult);
                    return directResult;
                }
            }
            catch (JsonException)
            {
                // Fall through to resilient sanitization
            }
        }

        // 2. Resilient Sanitization Path: Strip markdown code blocks & extraneous narrative text
        var cleanedJson = trimmed;
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            cleanedJson = cleanedJson[7..];
        else if (cleanedJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            cleanedJson = cleanedJson[3..];

        if (cleanedJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            cleanedJson = cleanedJson[..^3];

        cleanedJson = cleanedJson.Trim();

        var firstBrace = cleanedJson.IndexOf('{');
        var lastBrace = cleanedJson.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            cleanedJson = cleanedJson[firstBrace..(lastBrace + 1)];

        try
        {
            var result = JsonSerializer.Deserialize<IndianMealAnalysisResult>(cleanedJson, JsonOptions);

            // Handle alternate key aliases if model used 'items', 'dishes', or 'foodItems'
            if (result != null && (result.IdentifiedItems == null || result.IdentifiedItems.Count == 0))
            {
                using var document = JsonDocument.Parse(cleanedJson);
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var items) ||
                    root.TryGetProperty("dishes", out items) ||
                    root.TryGetProperty("foodItems", out items))
                {
                    result.IdentifiedItems = JsonSerializer.Deserialize<List<IndianMealItemDto>>(
                        items.GetRawText(),
                        JsonOptions) ?? new();
                }
            }

            if (result != null && result.IdentifiedItems is { Count: > 0 })
            {
                EnforceDeterministicTotals(result);
            }

            return result;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Failed to parse JSON response from model {Model}. Snippet: {Snippet}",
                modelId,
                cleanedJson.Length > 200 ? cleanedJson[..200] : cleanedJson);
            return null;
        }
    }

    private static bool IsValidMealResult(IndianMealAnalysisResult result)
    {
        return !string.IsNullOrWhiteSpace(result.DishName) &&
               result.IdentifiedItems != null &&
               result.IdentifiedItems.Count > 0;
    }

    private static void EnforceDeterministicTotals(IndianMealAnalysisResult result)
    {
        if (result.IdentifiedItems == null || result.IdentifiedItems.Count == 0) return;

        if (result.TotalCalories <= 0)
            result.TotalCalories = Math.Round(result.IdentifiedItems.Sum(i => i.Calories), 1);
        if (result.TotalProteinGrams <= 0)
            result.TotalProteinGrams = Math.Round(result.IdentifiedItems.Sum(i => i.ProteinGrams), 1);
        if (result.TotalCarbsGrams <= 0)
            result.TotalCarbsGrams = Math.Round(result.IdentifiedItems.Sum(i => i.CarbsGrams), 1);
        if (result.TotalFatGrams <= 0)
            result.TotalFatGrams = Math.Round(result.IdentifiedItems.Sum(i => i.FatGrams), 1);
        if (result.TotalFiberGrams <= 0)
            result.TotalFiberGrams = Math.Round(result.IdentifiedItems.Sum(i => i.FiberGrams), 1);
        if (result.TotalSugarGrams <= 0)
            result.TotalSugarGrams = Math.Round(result.IdentifiedItems.Sum(i => i.SugarGrams), 1);
        if (result.TotalSodiumMg <= 0)
            result.TotalSodiumMg = Math.Round(result.IdentifiedItems.Sum(i => i.SodiumMg), 1);
    }
}

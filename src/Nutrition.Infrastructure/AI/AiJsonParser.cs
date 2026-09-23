using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

internal static class AiJsonParser
{
    public static IndianMealAnalysisResult? Parse(string? responseText, ILogger logger, string modelId)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var cleanedJson = responseText.Trim();
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            cleanedJson = cleanedJson[7..];
        if (cleanedJson.StartsWith("```"))
            cleanedJson = cleanedJson[3..];
        if (cleanedJson.EndsWith("```"))
            cleanedJson = cleanedJson[..^3];
        cleanedJson = cleanedJson.Trim();

        var firstBrace = cleanedJson.IndexOf('{');
        var lastBrace = cleanedJson.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            cleanedJson = cleanedJson[firstBrace..(lastBrace + 1)];

        try
        {
            var result = JsonSerializer.Deserialize<IndianMealAnalysisResult>(
                cleanedJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.IdentifiedItems is { Count: 0 })
            {
                using var document = JsonDocument.Parse(cleanedJson);
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var items) ||
                    root.TryGetProperty("dishes", out items) ||
                    root.TryGetProperty("foodItems", out items))
                {
                    result.IdentifiedItems = JsonSerializer.Deserialize<List<IndianMealItemDto>>(
                        items.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }

            if (result != null && result.IdentifiedItems is { Count: > 0 })
            {
                if (result.TotalCalories <= 0)
                    result.TotalCalories = Math.Round(result.IdentifiedItems.Sum(i => i.Calories), 1);
                if (result.TotalProteinGrams <= 0)
                    result.TotalProteinGrams = Math.Round(result.IdentifiedItems.Sum(i => i.ProteinGrams), 1);
                if (result.TotalCarbsGrams <= 0)
                    result.TotalCarbsGrams = Math.Round(result.IdentifiedItems.Sum(i => i.CarbsGrams), 1);
                if (result.TotalFatGrams <= 0)
                    result.TotalFatGrams = Math.Round(result.IdentifiedItems.Sum(i => i.FatGrams), 1);
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
}

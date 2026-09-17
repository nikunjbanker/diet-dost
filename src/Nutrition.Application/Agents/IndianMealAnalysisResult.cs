using System.Text.Json.Serialization;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Agents;

public class IndianMealItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("hindiOrRegionalName")]
    public string HindiOrRegionalName { get; set; } = string.Empty;

    [JsonPropertyName("estimatedPortion")]
    public string EstimatedPortion { get; set; } = string.Empty;

    [JsonPropertyName("grams")]
    public double Grams { get; set; }

    [JsonPropertyName("calories")]
    public double Calories { get; set; }

    [JsonPropertyName("proteinGrams")]
    public double ProteinGrams { get; set; }

    [JsonPropertyName("carbsGrams")]
    public double CarbsGrams { get; set; }

    [JsonPropertyName("fatGrams")]
    public double FatGrams { get; set; }

    [JsonPropertyName("fiberGrams")]
    public double FiberGrams { get; set; }

    [JsonPropertyName("sodiumMg")]
    public double SodiumMg { get; set; }

    [JsonPropertyName("cookingMediumEstimate")]
    public string CookingMediumEstimate { get; set; } = string.Empty;

    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; } = 0.85;
}

public class IndianMealAnalysisResult
{
    [JsonPropertyName("mealType")]
    public string MealType { get; set; } = "Lunch";

    [JsonPropertyName("dishName")]
    public string DishName { get; set; } = string.Empty;

    [JsonPropertyName("overallConfidenceScore")]
    public double OverallConfidenceScore { get; set; } = 0.85;

    [JsonPropertyName("identifiedItems")]
    public List<IndianMealItemDto> IdentifiedItems { get; set; } = new();

    [JsonPropertyName("totalCalories")]
    public double TotalCalories { get; set; }

    [JsonPropertyName("totalProteinGrams")]
    public double TotalProteinGrams { get; set; }

    [JsonPropertyName("totalCarbsGrams")]
    public double TotalCarbsGrams { get; set; }

    [JsonPropertyName("totalFatGrams")]
    public double TotalFatGrams { get; set; }

    [JsonPropertyName("totalFiberGrams")]
    public double TotalFiberGrams { get; set; }

    [JsonPropertyName("totalSodiumMg")]
    public double TotalSodiumMg { get; set; }

    [JsonPropertyName("whoComplianceFlags")]
    public List<string> WhoComplianceFlags { get; set; } = new();

    [JsonPropertyName("medicationWarnings")]
    public List<string> MedicationWarnings { get; set; } = new();

    [JsonPropertyName("conditionSpecificAdvice")]
    public string? ConditionSpecificAdvice { get; set; }

    [JsonPropertyName("dietitianAdvice")]
    public string? DietitianAdvice { get; set; }

    [JsonPropertyName("detectedByModel")]
    public string? DetectedByModel { get; set; }

    public bool IsConfidenceGatedPassed => OverallConfidenceScore >= 0.70;
}

public interface IFoodVisionAgent
{
    Task<IndianMealAnalysisResult> AnalyzeMealPhotoAsync(
        Stream imageStream,
        string mimeType,
        string? regionalContext = null,
        UserProfile? userContext = null,
        List<Nutrition.Domain.Model.Meal.UserCorrectionRecord>? userLearnedCorrections = null,
        CancellationToken ct = default);

    Task<IndianMealAnalysisResult> AnalyzeMealDescriptionAsync(
        string description,
        string? mealType = null,
        UserProfile? userContext = null,
        List<Nutrition.Domain.Model.Meal.UserCorrectionRecord>? userLearnedCorrections = null,
        CancellationToken ct = default);
}

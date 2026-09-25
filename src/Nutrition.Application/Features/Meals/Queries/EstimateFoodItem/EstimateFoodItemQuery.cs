using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Meals.Queries.EstimateFoodItem;

public record EstimateFoodItemQuery(
    string CurrentUserId,
    string Name,
    string? Portion,
    bool UseAi,
    string? MealType
) : IQuery<Result<FoodItemNutritionEstimate>>;

public class EstimateFoodItemQueryHandler : IQueryHandler<EstimateFoodItemQuery, Result<FoodItemNutritionEstimate>>
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;

    public EstimateFoodItemQueryHandler(
        IFoodVisionAgent visionAgent,
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo)
    {
        _visionAgent = visionAgent;
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
    }

    public async Task<Result<FoodItemNutritionEstimate>> HandleAsync(EstimateFoodItemQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<FoodItemNutritionEstimate>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<FoodItemNutritionEstimate>.Failure("Food item name is required.", "MissingName", 400);

        UserProfile? userProfile = await _dietitianService.GetProfileAsync(request.CurrentUserId, ct);
        List<UserCorrectionRecord>? userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == request.CurrentUserId, ct);

        var tz = ClinicalDietitianService.GetUserTimeZoneInfo(userProfile?.Timezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var hour = localTime.Hour + (localTime.Minute / 60.0);
        var clockMealType = hour >= 5.0 && hour < 11.5 ? "Breakfast" :
                            hour >= 11.5 && hour < 16.0 ? "Lunch" :
                            hour >= 16.0 && hour < 19.5 ? "Snack" : "Dinner";

        var effectiveMealType = !string.IsNullOrWhiteSpace(request.MealType) ? request.MealType : clockMealType;

        var baseline = IndianFoodEstimator.Estimate(request.Name, request.Portion);

        if (request.UseAi)
        {
            try
            {
                var queryText = string.IsNullOrWhiteSpace(request.Portion)
                    ? request.Name
                    : $"{request.Portion} of {request.Name}";

                var aiResult = await _visionAgent.AnalyzeMealDescriptionAsync(queryText, effectiveMealType, userProfile, userCorrections, ct);
                var matched = aiResult?.IdentifiedItems?.FirstOrDefault();
                if (matched != null && matched.Calories > 0)
                {
                    var aiEstimate = new FoodItemNutritionEstimate(
                        NormalizedName: !string.IsNullOrWhiteSpace(matched.Name) ? matched.Name : baseline.NormalizedName,
                        HindiOrRegionalName: !string.IsNullOrWhiteSpace(matched.HindiOrRegionalName) ? matched.HindiOrRegionalName : baseline.HindiOrRegionalName,
                        EstimatedPortion: !string.IsNullOrWhiteSpace(matched.EstimatedPortion) ? matched.EstimatedPortion : (string.IsNullOrWhiteSpace(request.Portion) ? baseline.EstimatedPortion : request.Portion),
                        Grams: matched.Grams > 0 ? matched.Grams : baseline.Grams,
                        Calories: Math.Round(matched.Calories),
                        ProteinGrams: Math.Round(matched.ProteinGrams, 1),
                        CarbsGrams: Math.Round(matched.CarbsGrams, 1),
                        FatGrams: Math.Round(matched.FatGrams, 1),
                        FiberGrams: matched.FiberGrams > 0 ? Math.Round(matched.FiberGrams, 1) : baseline.FiberGrams,
                        SodiumMg: matched.SodiumMg > 0 ? Math.Round(matched.SodiumMg, 1) : baseline.SodiumMg,
                        CookingMediumEstimate: !string.IsNullOrWhiteSpace(matched.CookingMediumEstimate) ? matched.CookingMediumEstimate : baseline.CookingMediumEstimate,
                        Source: !string.IsNullOrWhiteSpace(aiResult?.DetectedByModel) ? $"AI ({aiResult.DetectedByModel} / Clinical NLP)" : "AI (Gemini 3.8 / Clinical NLP)",
                        ConfidenceScore: matched.ConfidenceScore > 0 ? matched.ConfidenceScore : (aiResult?.OverallConfidenceScore > 0 ? aiResult.OverallConfidenceScore : 0.90),
                        SugarGrams: matched.SugarGrams > 0 ? Math.Round(matched.SugarGrams, 1) : baseline.SugarGrams
                    );
                    return Result<FoodItemNutritionEstimate>.Success(aiEstimate);
                }
            }
            catch
            {
                // Fall back gracefully to baseline ICMR-NIN estimate
            }
        }

        return Result<FoodItemNutritionEstimate>.Success(baseline);
    }
}

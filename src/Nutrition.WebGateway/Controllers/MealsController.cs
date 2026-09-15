using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Controllers;

public record TextAnalysisRequest(string UserId, string Description, string? MealType);

[ApiController]
[Route("api/[controller]")]
public class MealsController : ControllerBase
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IUnitOfWork _uow;

    public MealsController(
        IFoodVisionAgent visionAgent,
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IUnitOfWork uow)
    {
        _visionAgent = visionAgent;
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
        _uow = uow;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ImageUploadValidator.MaxSizeBytes)]
    public async Task<IActionResult> UploadAndAnalyzeMeal(
        [FromForm] IFormFile? image,
        [FromForm] string userId,
        [FromForm] string? regionalContext,
        [FromForm] string? mealType,
        CancellationToken ct)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Please provide a valid meal photo." });

        using var stream = image.OpenReadStream();
        var (isValid, errorMessage, mimeType) = ImageUploadValidator.ValidateImage(stream, image.Length);
        if (!isValid)
            return BadRequest(new { error = errorMessage });

        UserProfile? userProfile = null;
        List<UserCorrectionRecord>? userCorrections = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            userProfile = await _dietitianService.GetProfileAsync(userId, ct);
            userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == userId, ct);
        }

        stream.Position = 0;
        var analysis = await _visionAgent.AnalyzeMealPhotoAsync(stream, mimeType!, regionalContext, userProfile, userCorrections, ct);

        // Check Confidence Gating Threshold (>= 70%)
        if (!analysis.IsConfidenceGatedPassed)
        {
            return Ok(new
            {
                confidenceGated = false,
                confidenceScore = analysis.OverallConfidenceScore,
                message = "The photo is too shadowy, blurry, or occluded to accurately identify portions.",
                advice = analysis.DietitianAdvice ?? "Please retake the photo with the plate centered under good lighting, or use 1-Tap Voice / Smart Search.",
                requiresRetake = true
            });
        }

        return Ok(new
        {
            confidenceGated = true,
            confidenceScore = analysis.OverallConfidenceScore,
            analysis
        });
    }

    [HttpPost("analyze-text")]
    public async Task<IActionResult> AnalyzeTextMeal([FromBody] TextAnalysisRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description cannot be empty." });

        UserProfile? userProfile = null;
        List<UserCorrectionRecord>? userCorrections = null;
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            userProfile = await _dietitianService.GetProfileAsync(request.UserId, ct);
            userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == request.UserId, ct);
        }

        var analysis = await _visionAgent.AnalyzeMealDescriptionAsync(request.Description, request.MealType, userProfile, userCorrections, ct);
        return Ok(new
        {
            confidenceGated = true,
            confidenceScore = analysis.OverallConfidenceScore,
            analysis
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmMeal([FromBody] MealLog meal, CancellationToken ct)
    {
        meal.IsVerifiedByUser = true;
        meal.LoggedAt = DateTime.UtcNow;

        // Continuous Model Re-Training & Adaptive Learning
        // Check if the user corrected any detected items
        var learnedNotes = new List<string>();
        foreach (var item in meal.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.OriginalDetection) && 
                !item.OriginalDetection.Trim().Equals(item.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var origKey = item.OriginalDetection.Trim();
                var correctedKey = item.Name.Trim();

                var existing = (await _correctionsRepo.FindAsync(
                    c => c.UserId == meal.UserId && c.OriginalDetectedItem.ToLower() == origKey.ToLower(), ct))
                    .FirstOrDefault();

                if (existing != null)
                {
                    existing.CorrectedItemName = correctedKey;
                    existing.HindiOrRegionalName = item.HindiOrRegionalName;
                    existing.EstimatedPortion = item.EstimatedPortion;
                    existing.Calories = item.Calories;
                    existing.ProteinGrams = item.ProteinGrams;
                    existing.CarbsGrams = item.CarbsGrams;
                    existing.FatGrams = item.FatGrams;
                    existing.MealType = meal.MealType.ToString();
                    existing.CreatedAtUtc = DateTime.UtcNow;
                    existing.FrequencyCount++;
                    await _correctionsRepo.UpdateAsync(existing, ct);
                }
                else
                {
                    var newCorrection = new UserCorrectionRecord
                    {
                        UserId = meal.UserId,
                        OriginalDetectedItem = origKey,
                        CorrectedItemName = correctedKey,
                        HindiOrRegionalName = item.HindiOrRegionalName,
                        EstimatedPortion = item.EstimatedPortion,
                        Calories = item.Calories,
                        ProteinGrams = item.ProteinGrams,
                        CarbsGrams = item.CarbsGrams,
                        FatGrams = item.FatGrams,
                        MealType = meal.MealType.ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        FrequencyCount = 1
                    };
                    await _correctionsRepo.AddAsync(newCorrection, ct);
                }

                learnedNotes.Add($"Diet Dost learned: '{origKey}' ➔ '{correctedKey}'");
            }
        }

        if (learnedNotes.Count > 0)
        {
            await _uow.SaveChangesAsync(ct);
        }

        meal.RecalculateTotals();
        var saved = await _dietitianService.LogMealAsync(meal, ct);
        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(meal.UserId, DateOnly.FromDateTime(meal.LoggedAt), ct);

        return Ok(new
        {
            meal = saved,
            dailyLedger = ledger,
            learnedNotes,
            learnedMessage = learnedNotes.Count > 0 ? string.Join(". ", learnedNotes) : null,
            message = "Meal logged successfully! Confetti burst triggered 🎉"
        });
    }

    [HttpGet("corrections")]
    public async Task<IActionResult> GetUserCorrections([FromQuery] string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "UserId is required." });

        var corrections = await _correctionsRepo.FindAsync(c => c.UserId == userId, ct);
        return Ok(corrections.OrderByDescending(c => c.CreatedAtUtc));
    }

    [HttpPost("estimate-item")]
    public async Task<IActionResult> EstimateFoodItem([FromBody] FoodItemEstimateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
            return BadRequest(new { error = "Food item name is required." });

        // 1. Get baseline ICMR-NIN estimate
        var baseline = IndianFoodEstimator.Estimate(request.Name, request.Portion);

        // 2. If AI is requested (default: true), attempt AI description analysis
        if (request.UseAi)
        {
            try
            {
                var queryText = string.IsNullOrWhiteSpace(request.Portion)
                    ? request.Name
                    : $"{request.Portion} of {request.Name}";

                var aiResult = await _visionAgent.AnalyzeMealDescriptionAsync(queryText, ct: ct);
                var matched = aiResult?.IdentifiedItems?.FirstOrDefault();
                if (matched != null && matched.Calories > 0)
                {
                    var aiEstimate = new FoodItemNutritionEstimate(
                        NormalizedName: !string.IsNullOrWhiteSpace(matched.Name) ? matched.Name : baseline.NormalizedName,
                        HindiOrRegionalName: !string.IsNullOrWhiteSpace(matched.HindiOrRegionalName) ? matched.HindiOrRegionalName : baseline.HindiOrRegionalName,
                        EstimatedPortion: !string.IsNullOrWhiteSpace(matched.EstimatedPortion) ? matched.EstimatedPortion : baseline.EstimatedPortion,
                        Grams: matched.Grams > 0 ? matched.Grams : baseline.Grams,
                        Calories: Math.Round(matched.Calories),
                        ProteinGrams: Math.Round(matched.ProteinGrams, 1),
                        CarbsGrams: Math.Round(matched.CarbsGrams, 1),
                        FatGrams: Math.Round(matched.FatGrams, 1),
                        FiberGrams: matched.FiberGrams > 0 ? Math.Round(matched.FiberGrams, 1) : baseline.FiberGrams,
                        SodiumMg: matched.SodiumMg > 0 ? Math.Round(matched.SodiumMg, 1) : baseline.SodiumMg,
                        CookingMediumEstimate: !string.IsNullOrWhiteSpace(matched.CookingMediumEstimate) ? matched.CookingMediumEstimate : baseline.CookingMediumEstimate,
                        Source: "AI (Gemini 3.8 / Clinical NLP)",
                        ConfidenceScore: matched.ConfidenceScore > 0 ? matched.ConfidenceScore : (aiResult.OverallConfidenceScore > 0 ? aiResult.OverallConfidenceScore : 0.90)
                    );
                    return Ok(aiEstimate);
                }
            }
            catch
            {
                // Fall back gracefully to baseline ICMR-NIN estimate
            }
        }

        return Ok(baseline);
    }
}

public record FoodItemEstimateRequest(string Name, string? Portion, bool UseAi = true);

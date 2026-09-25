using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

public record TextAnalysisRequest(string? UserId, string Description, string? MealType);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MealsController : ControllerBase
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IRepository<AiDetectionFeedbackRecord> _feedbackRepo;
    private readonly IAiQuotaService _quotaService;
    private readonly ITierConfigurationService _tierConfigService;
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _env;

    public MealsController(
        IFoodVisionAgent visionAgent,
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IRepository<AiDetectionFeedbackRecord> feedbackRepo,
        IAiQuotaService quotaService,
        ITierConfigurationService tierConfigService,
        IUnitOfWork uow,
        IWebHostEnvironment env)
    {
        _visionAgent = visionAgent;
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
        _feedbackRepo = feedbackRepo;
        _quotaService = quotaService;
        _tierConfigService = tierConfigService;
        _uow = uow;
        _env = env;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ImageUploadValidator.MaxSizeBytes)]
    public async Task<IActionResult> UploadAndAnalyzeMeal(
        [FromForm] IFormFile? image,
        [FromForm] string? userId,
        [FromForm] string? regionalContext,
        [FromForm] string? mealType,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        userId = currentUserId;

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

        // Tier Quota Gating (OWASP AI LLM04)
        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;
        var quota = await _quotaService.CheckQuotaAsync(currentUserId, userTier, userProfile?.Timezone, ct);
        if (!quota.IsAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "AiQuotaExceeded",
                message = quota.RejectionReason,
                tier = userTier.ToString(),
                usedToday = quota.UsedToday,
                dailyLimit = quota.DailyLimit,
                resetsAtUtc = quota.ResetsAtUtc
            });
        }

        stream.Position = 0;
        var effectiveMealType = !string.IsNullOrWhiteSpace(mealType) ? mealType : GetClockMealType(userProfile?.Timezone);
        var analysis = await _visionAgent.AnalyzeMealPhotoAsync(stream, mimeType!, regionalContext, userProfile, userCorrections, effectiveMealType, image.FileName, ct);

        // Record AI Usage Telemetry
        await _quotaService.RecordUsageAsync(
            currentUserId,
            AiOperationType.PhotoDetection,
            analysis.DetectedByModel ?? "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 1200,
            isSuccess: true,
            errorReason: null,
            ct: ct);

        // Auto-select Meal Type: If passed explicitly from client, use it; otherwise compute from user clock/timezone
        if (!string.IsNullOrWhiteSpace(mealType))
        {
            analysis.MealType = mealType;
        }
        else if (string.IsNullOrWhiteSpace(analysis.MealType))
        {
            analysis.MealType = effectiveMealType;
        }

        // Persist photo to wwwroot/uploads/meals for visual review & diary history
        string? photoUrl = null;
        try
        {
            var webRoot = !string.IsNullOrEmpty(_env.WebRootPath)
                ? _env.WebRootPath
                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRoot, "uploads", "meals");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileExtension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5)
            {
                fileExtension = mimeType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };
            }

            var uniqueFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{fileExtension}";
            var destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(destinationPath, FileMode.Create))
            {
                stream.Position = 0;
                await stream.CopyToAsync(fileStream, ct);
            }

            photoUrl = $"/uploads/meals/{uniqueFileName}";
            analysis.PhotoUri = photoUrl;
        }
        catch
        {
            // Non-blocking fallback if disk write fails
        }

        // Check Confidence Gating Threshold (>= 70%)
        if (!analysis.IsConfidenceGatedPassed)
        {
            await _quotaService.RecordUsageAsync(
                currentUserId,
                AiOperationType.PhotoDetection,
                analysis.DetectedByModel ?? "Gemini-3.8-Flash",
                estimatedTokens: 1200,
                latencyMs: 1100,
                isSuccess: false,
                errorReason: "ConfidenceGatedRetakeRequired",
                ct: ct);

            return Ok(new
            {
                confidenceGated = false,
                confidenceScore = analysis.OverallConfidenceScore,
                message = "The photo is too shadowy, blurry, or occluded to accurately identify portions.",
                advice = analysis.DietitianAdvice ?? "Please retake the photo with the plate centered under good lighting, or use 1-Tap Voice / Smart Search.",
                requiresRetake = true,
                photoUrl
            });
        }

        // Record successful AI Usage Telemetry
        await _quotaService.RecordUsageAsync(
            currentUserId,
            AiOperationType.PhotoDetection,
            analysis.DetectedByModel ?? "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 1100,
            isSuccess: true,
            errorReason: null,
            ct: ct);

        return Ok(new
        {
            confidenceGated = true,
            confidenceScore = analysis.OverallConfidenceScore,
            detectedByModel = analysis.DetectedByModel,
            photoUrl,
            analysis
        });
    }

    [HttpPost("analyze-text")]
    public async Task<IActionResult> AnalyzeTextMeal([FromBody] TextAnalysisRequest? request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (request == null || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description cannot be empty." });

        var effectiveUserId = currentUserId;
        var descTrimmed = request.Description.Trim();
        UserProfile? userProfile = await _dietitianService.GetProfileAsync(effectiveUserId, ct);
        List<UserCorrectionRecord>? userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);

        // Tier Quota Gating (OWASP AI LLM04)
        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;
        var quota = await _quotaService.CheckQuotaAsync(currentUserId, userTier, userProfile?.Timezone, ct);
        if (!quota.IsAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "AiQuotaExceeded",
                message = quota.RejectionReason,
                tier = userTier.ToString(),
                usedToday = quota.UsedToday,
                dailyLimit = quota.DailyLimit,
                resetsAtUtc = quota.ResetsAtUtc
            });
        }

        // Auto-select Meal Type: If text explicitly specifies, honor it; otherwise auto-detect from clock/timezone
        var clockMealType = GetClockMealType(userProfile?.Timezone);
        var effectiveMealType = !string.IsNullOrWhiteSpace(request.MealType) ? request.MealType : clockMealType;

        var descLower = descTrimmed.ToLowerInvariant();
        if (descLower.Contains("breakfast") || descLower.Contains("nashta") || descLower.Contains("nasta"))
        {
            effectiveMealType = "Breakfast";
        }
        else if (descLower.Contains("lunch") || descLower.Contains("dopahar"))
        {
            effectiveMealType = "Lunch";
        }
        else if (descLower.Contains("snack") || descLower.Contains("chai") || descLower.Contains("tea"))
        {
            effectiveMealType = "Snack";
        }
        else if (descLower.Contains("dinner") || descLower.Contains("raat"))
        {
            effectiveMealType = "Dinner";
        }

        var analysis = await _visionAgent.AnalyzeMealDescriptionAsync(descTrimmed, effectiveMealType, userProfile, userCorrections, ct);

        // Record AI Usage Telemetry
        await _quotaService.RecordUsageAsync(
            currentUserId,
            AiOperationType.TextDetection,
            analysis.DetectedByModel ?? "Gemini-3.8-Flash",
            estimatedTokens: 600,
            latencyMs: 750,
            isSuccess: true,
            errorReason: null,
            ct: ct);

        // Ensure analysis.MealType reflects the effectiveMealType if AI didn't explicitly override from text keywords
        if (string.IsNullOrWhiteSpace(analysis.MealType) ||
            (!descLower.Contains("breakfast") && !descLower.Contains("lunch") && !descLower.Contains("snack") && !descLower.Contains("dinner") &&
             !descLower.Contains("nashta") && !descLower.Contains("dopahar") && !descLower.Contains("raat") && !descLower.Contains("chai")))
        {
            analysis.MealType = effectiveMealType;
        }

        return Ok(new
        {
            confidenceGated = true,
            confidenceScore = analysis.OverallConfidenceScore,
            detectedByModel = analysis.DetectedByModel,
            analysis
        });
    }

    /// <summary>
    /// Computes the auto-detected meal type based on local time in the user's timezone.
    /// Follows Indian dining habits and ICMR-NIN 2024 circadian rhythm distribution:
    /// - 05:00 to 11:30: Breakfast
    /// - 11:30 to 16:00: Lunch
    /// - 16:00 to 19:30: Snack
    /// - 19:30 to 05:00: Dinner
    /// </summary>
    private static string GetClockMealType(string? timezoneId)
    {
        var tz = ClinicalDietitianService.GetUserTimeZoneInfo(timezoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var hour = localTime.Hour + (localTime.Minute / 60.0);

        if (hour >= 5.0 && hour < 11.5) return "Breakfast";
        if (hour >= 11.5 && hour < 16.0) return "Lunch";
        if (hour >= 16.0 && hour < 19.5) return "Snack";
        return "Dinner";
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmMeal([FromBody] MealLog meal, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // Enforce tenant isolation: meal belongs to authenticated user unless admin explicitly overrides
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            meal.UserId = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(meal.UserId))
        {
            meal.UserId = currentUserId;
        }

        meal.IsVerifiedByUser = true;
        meal.LoggedAt = meal.LoggedAt != default ? meal.LoggedAt.ToUniversalTime() : DateTime.UtcNow;

        // Continuous Model Re-Training & Adaptive Learning
        // Check if the user corrected any genuinely detected items
        var learnedNotes = new List<string>();
        foreach (var item in meal.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.OriginalDetection) && 
                !item.OriginalDetection.Trim().Equals(item.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var origKey = item.OriginalDetection.Trim();
                var correctedKey = item.Name.Trim();

                // Skip manual additions by user (e.g. "Added by User", "+ Add Dish") - these are additions, not detection corrections
                if (origKey.StartsWith("Added by", StringComparison.OrdinalIgnoreCase) ||
                    origKey.Contains("Added by", StringComparison.OrdinalIgnoreCase) ||
                    correctedKey.StartsWith("Added by", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // If user is correcting A -> B, purge any existing contradictory/cyclic rules (e.g. B -> A)
                var contradictory = await _correctionsRepo.FindAsync(
                    c => c.UserId == meal.UserId && 
                         ((c.OriginalDetectedItem.ToLower() == correctedKey.ToLower() && c.CorrectedItemName.ToLower() == origKey.ToLower()) ||
                          (c.OriginalDetectedItem.ToLower().Contains(correctedKey.ToLower()) && c.CorrectedItemName.ToLower().Contains(origKey.ToLower()))), 
                    ct);

                foreach (var contra in contradictory)
                {
                    await _correctionsRepo.DeleteAsync(contra.Id, ct);
                }

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMealById([FromRoute] string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Meal id is required." });

        var meal = await _dietitianService.GetMealByIdAsync(id, ct);
        if (meal is null)
            return NotFound(new { error = $"Meal with ID '{id}' was not found." });

        if (meal.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            return Forbid();

        return Ok(meal);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMeal([FromRoute] string id, [FromBody] MealLog meal, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Meal id is required." });

        if (meal is null)
            return BadRequest(new { error = "Meal payload cannot be null." });

        var existing = await _dietitianService.GetMealByIdAsync(id, ct);
        if (existing is null)
            return NotFound(new { error = $"Meal with ID '{id}' was not found." });

        if (existing.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            return Forbid();

        meal.Id = id;
        meal.UserId = existing.UserId;
        var updated = await _dietitianService.UpdateMealAsync(meal, ct);
        if (updated is null)
            return NotFound(new { error = $"Meal with ID '{id}' was not found." });

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(updated.UserId, DateOnly.FromDateTime(updated.LoggedAt), ct);

        return Ok(new
        {
            meal = updated,
            dailyLedger = ledger,
            message = "Meal updated successfully! Daily ledger and trends synchronized ✨"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeal([FromRoute] string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Meal id is required." });

        var existing = await _dietitianService.GetMealByIdAsync(id, ct);
        if (existing is null)
            return NotFound(new { error = $"Meal with ID '{id}' was not found." });

        if (existing.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            return Forbid();

        var success = await _dietitianService.DeleteMealAsync(id, ct);
        if (!success)
            return NotFound(new { error = $"Meal with ID '{id}' was not found." });

        return Ok(new
        {
            success = true,
            message = "Meal deleted successfully! Daily ledger recalculated 🗑️"
        });
    }

    [HttpGet("corrections")]
    public async Task<IActionResult> GetUserCorrections([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(userId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? userId
            : currentUserId;

        var corrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);
        return Ok(corrections.OrderByDescending(c => c.CreatedAtUtc));
    }

    [HttpDelete("corrections/reset")]
    public async Task<IActionResult> ResetUserCorrections([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(userId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? userId
            : currentUserId;

        var corrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);
        foreach (var c in corrections)
        {
            await _correctionsRepo.DeleteAsync(c.Id, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return Ok(new { message = $"Cleared {corrections.Count} trained memory corrections for user {effectiveUserId}." });
    }

    [HttpDelete("corrections/{id}")]
    public async Task<IActionResult> DeleteCorrection(string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Id is required." });

        var existing = (await _correctionsRepo.FindAsync(c => c.Id == id, ct)).FirstOrDefault();
        if (existing == null)
            return NotFound(new { error = "Correction not found." });

        if (existing.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
            return Forbid();

        await _correctionsRepo.DeleteAsync(id, ct);
        await _uow.SaveChangesAsync(ct);
        return Ok(new { message = "Correction deleted successfully." });
    }

    [HttpPost("ai-feedback")]
    public async Task<IActionResult> SubmitAiFeedback([FromBody] AiFeedbackRequest request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (request == null)
            return BadRequest(new { error = "Feedback details are required." });

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.UserId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? request.UserId
            : currentUserId;

        using var activity = NutritionTelemetry.ActivitySource.StartActivity("diet.ai_feedback", System.Diagnostics.ActivityKind.Server);
        activity?.SetTag("diet.feedback.rating", request.Rating);
        activity?.SetTag("diet.feedback.model", request.DetectedByModel);
        activity?.SetTag("diet.feedback.dish", request.DishName);

        var feedbackRecord = new AiDetectionFeedbackRecord
        {
            UserId = effectiveUserId,
            MealLogId = request.MealLogId,
            DishName = request.DishName,
            DetectedByModel = request.DetectedByModel,
            ConfidenceScore = request.ConfidenceScore,
            Rating = request.Rating,
            Remarks = request.Remarks,
            IdentifiedItemsSummary = request.Items != null ? string.Join("; ", request.Items.Select(i => $"{i.Name} ({i.EstimatedPortion})")) : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        var retrainingResult = await _visionAgent.ProcessFeedbackRetrainingAsync(
            effectiveUserId,
            request.DishName,
            request.Rating,
            request.Remarks,
            request.Items,
            ct);

        feedbackRecord.RetrainingTriggered = retrainingResult.Retrained;
        feedbackRecord.RetrainingOutcome = retrainingResult.Message;

        // If retraining identified a genuine dish correction, persist it to UserCorrectionRecord as well
        if (retrainingResult.Retrained &&
            !string.IsNullOrWhiteSpace(retrainingResult.OriginalDetectedDish) &&
            !string.IsNullOrWhiteSpace(retrainingResult.CorrectedDish) &&
            !retrainingResult.OriginalDetectedDish.Equals(retrainingResult.CorrectedDish, StringComparison.OrdinalIgnoreCase))
        {
            var origKey = retrainingResult.OriginalDetectedDish.Trim();
            var correctedKey = retrainingResult.CorrectedDish.Trim();

            // Purge contradictory mappings
            var contradictory = await _correctionsRepo.FindAsync(
                c => c.UserId == effectiveUserId &&
                     ((c.OriginalDetectedItem.ToLower() == correctedKey.ToLower() && c.CorrectedItemName.ToLower() == origKey.ToLower()) ||
                      (c.OriginalDetectedItem.ToLower().Contains(correctedKey.ToLower()) && c.CorrectedItemName.ToLower().Contains(origKey.ToLower()))),
                ct);

            foreach (var contra in contradictory)
            {
                await _correctionsRepo.DeleteAsync(contra.Id, ct);
            }

            var existing = (await _correctionsRepo.FindAsync(
                c => c.UserId == effectiveUserId && c.OriginalDetectedItem.ToLower() == origKey.ToLower(), ct))
                .FirstOrDefault();

            var updatedEstimate = retrainingResult.UpdatedItemEstimate;

            if (existing != null)
            {
                existing.CorrectedItemName = correctedKey;
                existing.HindiOrRegionalName = updatedEstimate?.HindiOrRegionalName ?? correctedKey;
                existing.EstimatedPortion = updatedEstimate?.EstimatedPortion ?? "1 Katori";
                existing.Calories = updatedEstimate?.Calories ?? 120;
                existing.ProteinGrams = updatedEstimate?.ProteinGrams ?? 3;
                existing.CarbsGrams = updatedEstimate?.CarbsGrams ?? 10;
                existing.FatGrams = updatedEstimate?.FatGrams ?? 6;
                existing.CreatedAtUtc = DateTime.UtcNow;
                existing.FrequencyCount++;
                await _correctionsRepo.UpdateAsync(existing, ct);
            }
            else
            {
                var newCorrection = new UserCorrectionRecord
                {
                    UserId = effectiveUserId,
                    OriginalDetectedItem = origKey,
                    CorrectedItemName = correctedKey,
                    HindiOrRegionalName = updatedEstimate?.HindiOrRegionalName ?? correctedKey,
                    EstimatedPortion = updatedEstimate?.EstimatedPortion ?? "1 Katori",
                    Calories = updatedEstimate?.Calories ?? 120,
                    ProteinGrams = updatedEstimate?.ProteinGrams ?? 3,
                    CarbsGrams = updatedEstimate?.CarbsGrams ?? 10,
                    FatGrams = updatedEstimate?.FatGrams ?? 6,
                    MealType = "Lunch",
                    CreatedAtUtc = DateTime.UtcNow,
                    FrequencyCount = 1
                };
                await _correctionsRepo.AddAsync(newCorrection, ct);
            }
        }

        await _feedbackRepo.AddAsync(feedbackRecord, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            retrained = retrainingResult.Retrained,
            message = retrainingResult.Message,
            originalDetectedDish = retrainingResult.OriginalDetectedDish,
            correctedDish = retrainingResult.CorrectedDish,
            updatedItemEstimate = retrainingResult.UpdatedItemEstimate
        });
    }

    [HttpGet("ai-feedback")]
    public async Task<IActionResult> GetAiFeedbacks([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(userId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? userId
            : currentUserId;

        var feedbacks = await _feedbackRepo.FindAsync(f => f.UserId == effectiveUserId, ct);
        return Ok(feedbacks.OrderByDescending(f => f.CreatedAtUtc));
    }

    [HttpPost("estimate-item")]
    public async Task<IActionResult> EstimateFoodItem([FromBody] FoodItemEstimateRequest request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Name))
            return BadRequest(new { error = "Food item name is required." });

        var effectiveUserId = currentUserId;
        UserProfile? userProfile = await _dietitianService.GetProfileAsync(effectiveUserId, ct);
        List<UserCorrectionRecord>? userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);

        var effectiveMealType = !string.IsNullOrWhiteSpace(request.MealType) ? request.MealType : GetClockMealType(userProfile?.Timezone);

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

    [HttpGet("history")]
    public async Task<IActionResult> GetMealHistory(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        [FromQuery] string? date = null,
        [FromQuery] string? mealType = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(userId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? userId
            : currentUserId;

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d))
        {
            parsedDate = d;
        }

        MealType? parsedMealType = null;
        if (!string.IsNullOrWhiteSpace(mealType) && Enum.TryParse<MealType>(mealType, true, out var mt))
        {
            parsedMealType = mt;
        }

        var meals = await _dietitianService.GetMealHistoryAsync(effectiveUserId, period, parsedDate, parsedMealType, ct);

        // Aggregate summary metrics
        double totalCalories = Math.Round(meals.Sum(m => m.TotalCalories), 1);
        double totalProtein = Math.Round(meals.Sum(m => m.TotalProteinGrams), 1);
        double totalCarbs = Math.Round(meals.Sum(m => m.TotalCarbsGrams), 1);
        double totalFat = Math.Round(meals.Sum(m => m.TotalFatGrams), 1);
        double totalFiber = Math.Round(meals.Sum(m => m.TotalFiberGrams), 1);
        double totalSugar = Math.Round(meals.Sum(m => m.TotalSugarGrams), 1);
        double totalSodium = Math.Round(meals.Sum(m => m.TotalSodiumMg), 1);

        return Ok(new
        {
            userId = effectiveUserId,
            period,
            selectedDate = parsedDate?.ToString("yyyy-MM-dd"),
            mealType = parsedMealType?.ToString(),
            totalMealsCount = meals.Count,
            summary = new
            {
                totalCalories,
                totalProteinGrams = totalProtein,
                totalCarbsGrams = totalCarbs,
                totalFatGrams = totalFat,
                totalFiberGrams = totalFiber,
                totalSugarGrams = totalSugar,
                totalSodiumMg = totalSodium
            },
            meals
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportMealHistory(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        [FromQuery] string? date = null,
        [FromQuery] string? mealType = null,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // Tier Feature Check: Excel / CSV Export
        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;
        var tierConfig = await _tierConfigService.GetConfigurationAsync(userTier, ct);

        if (!tierConfig.AllowDataExport && !User.IsAdminOrSuper())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureTierUpgradeRequired",
                message = "Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan."
            });
        }

        var effectiveUserId = (!string.IsNullOrWhiteSpace(userId) && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            ? userId
            : currentUserId;

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d))
        {
            parsedDate = d;
        }

        MealType? parsedMealType = null;
        if (!string.IsNullOrWhiteSpace(mealType) && Enum.TryParse<MealType>(mealType, true, out var mt))
        {
            parsedMealType = mt;
        }

        var meals = await _dietitianService.GetMealHistoryAsync(effectiveUserId, period, parsedDate, parsedMealType, ct);

        // Build RFC 4180 compliant CSV with UTF-8 BOM (\uFEFF) for immediate native Microsoft Excel compatibility
        var sb = new System.Text.StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 Byte Order Mark for Excel

        // CSV Header
        sb.AppendLine("Meal ID,Date,Time,Meal Type,Dish Name,Calories (kcal),Protein (g),Carbs (g),Fat (g),Fiber (g),Sugar (g),Sodium (mg),Ghee/Tadka (kcal),Food Items Breakdown,AI Confidence,Verified By User,Dietitian Clinical Advice,Feedback Rating,Feedback Remarks");

        static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        foreach (var meal in meals)
        {
            var localTime = meal.LoggedAt.ToLocalTime();
            var dateStr = localTime.ToString("yyyy-MM-dd");
            var timeStr = localTime.ToString("HH:mm:ss");

            var itemsBreakdown = string.Join("; ", meal.Items.Select(i =>
                $"{i.Name} [Portion: {i.EstimatedPortion}, Qty: {i.Quantity}, Kcal: {i.Calories}, P: {i.ProteinGrams}g, C: {i.CarbsGrams}g, F: {i.FatGrams}g, Fib: {i.FiberGrams}g, Sug: {i.SugarGrams}g, Sod: {i.SodiumMg}mg]"));

            var addedCookingFat = Math.Round(meal.AddedGheeKcal + meal.AddedTadkaKcal, 1);

            sb.AppendLine(string.Join(",",
                EscapeCsv(meal.Id),
                EscapeCsv(dateStr),
                EscapeCsv(timeStr),
                EscapeCsv(meal.MealType.ToString()),
                EscapeCsv(meal.DishName),
                meal.TotalCalories.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalProteinGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalCarbsGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalFatGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalFiberGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalSugarGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalSodiumMg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                addedCookingFat.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                EscapeCsv(itemsBreakdown),
                meal.OverallConfidenceScore.ToString("P0", System.Globalization.CultureInfo.InvariantCulture),
                meal.IsVerifiedByUser ? "Yes" : "No",
                EscapeCsv(meal.DietitianAdvice),
                EscapeCsv(meal.AiFeedbackRating),
                EscapeCsv(meal.AiFeedbackRemarks)
            ));
        }

        var csvBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"DietDost_Meals_{period}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("quota")]
    public async Task<IActionResult> GetAiQuota(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var profile = await _dietitianService.GetProfileAsync(currentUserId, ct);
        var stats = await _quotaService.GetUsageStatsAsync(currentUserId, userTier, profile?.Timezone, ct);
        return Ok(stats);
    }
}

public record FoodItemEstimateRequest(string Name, string? Portion, bool UseAi = true, string? UserId = null, string? MealType = null);

public record AiFeedbackRequest(
    string UserId,
    string? MealLogId,
    string DishName,
    string DetectedByModel,
    double ConfidenceScore,
    string Rating,
    string? Remarks,
    List<IndianMealItemDto>? Items = null
);

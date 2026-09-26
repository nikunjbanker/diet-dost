/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Meals.Commands.AnalyzeTextMeal;

public record AnalyzeTextMealCommand(
    string UserId,
    UserTier Tier,
    string Description,
    string? MealType
) : ICommand<Result<MealAnalysisResultDto>>;

public class AnalyzeTextMealCommandHandler : ICommandHandler<AnalyzeTextMealCommand, Result<MealAnalysisResultDto>>
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IAiQuotaService _quotaService;

    public AnalyzeTextMealCommandHandler(
        IFoodVisionAgent visionAgent,
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IAiQuotaService quotaService)
    {
        _visionAgent = visionAgent;
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
        _quotaService = quotaService;
    }

    public async Task<Result<MealAnalysisResultDto>> HandleAsync(AnalyzeTextMealCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<MealAnalysisResultDto>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<MealAnalysisResultDto>.Failure("Description cannot be empty.", "MissingDescription", 400);

        var descTrimmed = request.Description.Trim();
        UserProfile? userProfile = await _dietitianService.GetProfileAsync(request.UserId, ct);
        List<UserCorrectionRecord>? userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == request.UserId, ct);

        // Tier Quota Gating (OWASP AI LLM04)
        var quota = await _quotaService.CheckQuotaAsync(request.UserId, request.Tier, userProfile?.Timezone, ct);
        if (!quota.IsAllowed)
        {
            return Result<MealAnalysisResultDto>.Failure(
                quota.RejectionReason ?? "Daily AI detection limit reached for your tier.",
                "AiQuotaExceeded",
                403);
        }

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
            request.UserId,
            AiOperationType.TextDetection,
            analysis.DetectedByModel ?? "Gemini-3.8-Flash",
            estimatedTokens: 600,
            latencyMs: 750,
            isSuccess: true,
            errorReason: null,
            ct: ct);

        if (string.IsNullOrWhiteSpace(analysis.MealType) ||
            (!descLower.Contains("breakfast") && !descLower.Contains("lunch") && !descLower.Contains("snack") && !descLower.Contains("dinner") &&
             !descLower.Contains("nashta") && !descLower.Contains("dopahar") && !descLower.Contains("raat") && !descLower.Contains("chai")))
        {
            analysis.MealType = effectiveMealType;
        }

        return Result<MealAnalysisResultDto>.Success(new MealAnalysisResultDto(
            true,
            analysis.OverallConfidenceScore,
            analysis.DetectedByModel,
            null,
            null,
            null,
            false,
            analysis
        ));
    }

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
}

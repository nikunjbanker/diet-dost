/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Meals.Commands.UploadAndAnalyzeMeal;

public record UploadAndAnalyzeMealCommand(
    string UserId,
    UserTier Tier,
    Stream ImageStream,
    string FileName,
    string MimeType,
    string? RegionalContext,
    string? MealType
) : ICommand<Result<MealAnalysisResultDto>>;

public class UploadAndAnalyzeMealCommandHandler : ICommandHandler<UploadAndAnalyzeMealCommand, Result<MealAnalysisResultDto>>
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IAiQuotaService _quotaService;
    private readonly IPhotoStorageService _photoStorageService;
    private readonly ILogger<UploadAndAnalyzeMealCommandHandler> _logger;

    public UploadAndAnalyzeMealCommandHandler(
        IFoodVisionAgent visionAgent,
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IAiQuotaService quotaService,
        IPhotoStorageService photoStorageService,
        ILogger<UploadAndAnalyzeMealCommandHandler> logger)
    {
        _visionAgent = visionAgent;
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
        _quotaService = quotaService;
        _photoStorageService = photoStorageService;
        _logger = logger;
    }

    public async Task<Result<MealAnalysisResultDto>> HandleAsync(UploadAndAnalyzeMealCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<MealAnalysisResultDto>.Unauthorized();

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

        request.ImageStream.Position = 0;
        var effectiveMealType = !string.IsNullOrWhiteSpace(request.MealType)
            ? request.MealType
            : GetClockMealType(userProfile?.Timezone);

        var analysis = await _visionAgent.AnalyzeMealPhotoAsync(
            request.ImageStream,
            request.MimeType,
            request.RegionalContext,
            userProfile,
            userCorrections,
            effectiveMealType,
            request.FileName,
            ct);

        // Auto-select Meal Type
        if (!string.IsNullOrWhiteSpace(request.MealType))
        {
            analysis.MealType = request.MealType;
        }
        else if (string.IsNullOrWhiteSpace(analysis.MealType))
        {
            analysis.MealType = effectiveMealType;
        }

        // Persist photo using Port abstraction
        string? photoUrl = null;
        try
        {
            request.ImageStream.Position = 0;
            photoUrl = await _photoStorageService.SaveMealPhotoAsync(request.ImageStream, request.FileName, request.MimeType, ct);
            analysis.PhotoUri = photoUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist meal photo for user: {UserId}", request.UserId);
        }

        // Confidence Gating Threshold Check (>= 70%)
        if (!analysis.IsConfidenceGatedPassed)
        {
            await _quotaService.RecordUsageAsync(
                request.UserId,
                AiOperationType.PhotoDetection,
                analysis.DetectedByModel ?? "Gemini-3.8-Flash",
                estimatedTokens: 1200,
                latencyMs: 1100,
                isSuccess: false,
                errorReason: "ConfidenceGatedRetakeRequired",
                ct: ct);

            return Result<MealAnalysisResultDto>.Success(new MealAnalysisResultDto(
                false,
                analysis.OverallConfidenceScore,
                analysis.DetectedByModel,
                photoUrl,
                "The photo is too shadowy, blurry, or occluded to accurately identify portions.",
                analysis.DietitianAdvice ?? "Please retake the photo with the plate centered under good lighting, or use 1-Tap Voice / Smart Search.",
                true,
                analysis
            ));
        }

        // Record AI Usage Telemetry
        await _quotaService.RecordUsageAsync(
            request.UserId,
            AiOperationType.PhotoDetection,
            analysis.DetectedByModel ?? "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 1100,
            isSuccess: true,
            errorReason: null,
            ct: ct);

        return Result<MealAnalysisResultDto>.Success(new MealAnalysisResultDto(
            true,
            analysis.OverallConfidenceScore,
            analysis.DetectedByModel,
            photoUrl,
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

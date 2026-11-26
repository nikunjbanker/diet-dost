/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.ComponentModel.DataAnnotations;
using Nutrition.Application.Agents;
using Nutrition.Application.Features.Analytics.DTOs;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.DTOs;

public sealed class FoodItemRequestDto
{
    public string? Id { get; set; }
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(200)]
    public string OriginalDetection { get; set; } = string.Empty;
    [MaxLength(200)]
    public string HindiOrRegionalName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string EstimatedPortion { get; set; } = "1 Katori";
    public double Quantity { get; set; } = 1.0;
    public double Grams { get; set; }
    public double Calories { get; set; }
    public double ProteinGrams { get; set; }
    public double CarbsGrams { get; set; }
    public double FatGrams { get; set; }
    public double FiberGrams { get; set; }
    public double SugarGrams { get; set; }
    public double SodiumMg { get; set; }
    [MaxLength(120)]
    public string CookingMediumEstimate { get; set; } = "Standard Home Cooking";
    public double ConfidenceScore { get; set; } = 0.85;
}

public sealed class SaveMealRequestDto
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public DateTime? LoggedAt { get; set; }
    public MealType MealType { get; set; } = MealType.Lunch;
    [MaxLength(200)]
    public string DishName { get; set; } = string.Empty;
    [MaxLength(512)]
    public string? PhotoUri { get; set; }
    public double OverallConfidenceScore { get; set; } = 0.85;
    public double AddedGheeKcal { get; set; }
    public double AddedTadkaKcal { get; set; }
    [MaxLength(128)]
    public string? ClientMutationId { get; set; }
    public DateTime? ClientTimestampUtc { get; set; }
    [MaxLength(50)]
    public List<FoodItemRequestDto> Items { get; set; } = new();
    [MaxLength(2000)]
    public string? ConditionSpecificAdvice { get; set; }
    [MaxLength(2000)]
    public string? DietitianAdvice { get; set; }
    [MaxLength(64)]
    public string? AiFeedbackRating { get; set; }
    [MaxLength(1000)]
    public string? AiFeedbackRemarks { get; set; }
}

public sealed record FoodItemDto(
    string Id,
    string MealLogId,
    string Name,
    string OriginalDetection,
    string HindiOrRegionalName,
    string EstimatedPortion,
    double Quantity,
    double Grams,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams,
    double SugarGrams,
    double SodiumMg,
    string CookingMediumEstimate,
    double ConfidenceScore
);

public sealed record MealLogDto(
    string Id,
    string UserId,
    DateTime LoggedAt,
    MealType MealType,
    string DishName,
    string? PhotoUri,
    double OverallConfidenceScore,
    bool IsConfidencePassed,
    List<FoodItemDto> Items,
    double AddedGheeKcal,
    double AddedTadkaKcal,
    string? ClientMutationId,
    DateTime? ClientTimestampUtc,
    double TotalCalories,
    double TotalProteinGrams,
    double TotalCarbsGrams,
    double TotalFatGrams,
    double TotalFiberGrams,
    double TotalSugarGrams,
    double TotalSodiumMg,
    List<string> WhoComplianceFlags,
    List<string> MedicationWarnings,
    string? ConditionSpecificAdvice,
    string? DietitianAdvice,
    bool IsVerifiedByUser,
    string? AiFeedbackRating,
    string? AiFeedbackRemarks
);

public sealed record UserCorrectionDto(
    string Id,
    string UserId,
    string OriginalDetectedItem,
    string CorrectedItemName,
    string HindiOrRegionalName,
    string EstimatedPortion,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    string MealType,
    DateTime CreatedAtUtc,
    int FrequencyCount
);

public sealed record AiDetectionFeedbackDto(
    string Id,
    string UserId,
    string? MealLogId,
    string DishName,
    string DetectedByModel,
    double ConfidenceScore,
    string Rating,
    string? Remarks,
    string? IdentifiedItemsSummary,
    bool RetrainingTriggered,
    string? RetrainingOutcome,
    DateTime CreatedAtUtc
);

public record MealAnalysisResultDto(
    bool ConfidenceGated,
    double ConfidenceScore,
    string? DetectedByModel,
    string? PhotoUrl,
    string? Message,
    string? Advice,
    bool RequiresRetake,
    IndianMealAnalysisResult? Analysis
);

public record ConfirmMealResultDto(
    MealLogDto Meal,
    DailyCalorieLedgerDto DailyLedger,
    List<string> LearnedNotes,
    string? LearnedMessage,
    string Message
);

public record UpdateMealResultDto(
    MealLogDto Meal,
    DailyCalorieLedgerDto DailyLedger,
    string Message
);

public record DeleteMealResultDto(
    bool Success,
    string Message
);

public static class MealDtoMapper
{
    public static FoodItemDto ToDto(this FoodItemRecord item) => new(
        item.Id,
        item.MealLogId,
        item.Name,
        item.OriginalDetection,
        item.HindiOrRegionalName,
        item.EstimatedPortion,
        item.Quantity,
        item.Grams,
        item.Calories,
        item.ProteinGrams,
        item.CarbsGrams,
        item.FatGrams,
        item.FiberGrams,
        item.SugarGrams,
        item.SodiumMg,
        item.CookingMediumEstimate,
        item.ConfidenceScore
    );

    public static MealLogDto ToDto(this MealLog meal) => new(
        meal.Id,
        meal.UserId,
        meal.LoggedAt,
        meal.MealType,
        meal.DishName,
        meal.PhotoUri,
        meal.OverallConfidenceScore,
        meal.IsConfidencePassed,
        meal.Items.Select(i => i.ToDto()).ToList(),
        meal.AddedGheeKcal,
        meal.AddedTadkaKcal,
        meal.ClientMutationId,
        meal.ClientTimestampUtc,
        meal.TotalCalories,
        meal.TotalProteinGrams,
        meal.TotalCarbsGrams,
        meal.TotalFatGrams,
        meal.TotalFiberGrams,
        meal.TotalSugarGrams,
        meal.TotalSodiumMg,
        meal.WhoComplianceFlags.ToList(),
        meal.MedicationWarnings.ToList(),
        meal.ConditionSpecificAdvice,
        meal.DietitianAdvice,
        meal.IsVerifiedByUser,
        meal.AiFeedbackRating,
        meal.AiFeedbackRemarks
    );

    public static MealLog ToNewDomainEntity(this SaveMealRequestDto dto, string effectiveUserId)
    {
        var mealId = Guid.NewGuid().ToString();
        var meal = new MealLog
        {
            Id = mealId,
            UserId = effectiveUserId,
            LoggedAt = dto.LoggedAt.HasValue && dto.LoggedAt.Value != default ? dto.LoggedAt.Value.ToUniversalTime() : DateTime.UtcNow,
            MealType = dto.MealType,
            DishName = dto.DishName?.Trim() ?? string.Empty,
            PhotoUri = dto.PhotoUri,
            OverallConfidenceScore = dto.OverallConfidenceScore,
            AddedGheeKcal = dto.AddedGheeKcal,
            AddedTadkaKcal = dto.AddedTadkaKcal,
            ClientMutationId = dto.ClientMutationId,
            ClientTimestampUtc = dto.ClientTimestampUtc,
            ConditionSpecificAdvice = dto.ConditionSpecificAdvice,
            DietitianAdvice = dto.DietitianAdvice,
            AiFeedbackRating = dto.AiFeedbackRating,
            AiFeedbackRemarks = dto.AiFeedbackRemarks,
            IsVerifiedByUser = true,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = (dto.Items ?? new List<FoodItemRequestDto>()).Select(i => new FoodItemRecord
            {
                Id = Guid.NewGuid().ToString(),
                MealLogId = mealId,
                Name = i.Name?.Trim() ?? string.Empty,
                OriginalDetection = i.OriginalDetection?.Trim() ?? string.Empty,
                HindiOrRegionalName = i.HindiOrRegionalName?.Trim() ?? string.Empty,
                EstimatedPortion = string.IsNullOrWhiteSpace(i.EstimatedPortion) ? "1 Katori" : i.EstimatedPortion.Trim(),
                Quantity = i.Quantity,
                Grams = i.Grams,
                Calories = i.Calories,
                ProteinGrams = i.ProteinGrams,
                CarbsGrams = i.CarbsGrams,
                FatGrams = i.FatGrams,
                FiberGrams = i.FiberGrams,
                SugarGrams = i.SugarGrams,
                SodiumMg = i.SodiumMg,
                CookingMediumEstimate = string.IsNullOrWhiteSpace(i.CookingMediumEstimate) ? "Standard Home Cooking" : i.CookingMediumEstimate.Trim(),
                ConfidenceScore = i.ConfidenceScore > 0 ? i.ConfidenceScore : 0.85
            }).ToList()
        };

        return meal;
    }

    public static MealLog ToUpdateDomainEntity(this SaveMealRequestDto dto, string mealId, string ownerUserId)
    {
        return new MealLog
        {
            Id = mealId,
            UserId = ownerUserId,
            LoggedAt = dto.LoggedAt ?? default,
            MealType = dto.MealType,
            DishName = dto.DishName?.Trim() ?? string.Empty,
            PhotoUri = dto.PhotoUri,
            OverallConfidenceScore = dto.OverallConfidenceScore,
            AddedGheeKcal = dto.AddedGheeKcal,
            AddedTadkaKcal = dto.AddedTadkaKcal,
            DietitianAdvice = dto.DietitianAdvice,
            AiFeedbackRating = dto.AiFeedbackRating,
            AiFeedbackRemarks = dto.AiFeedbackRemarks,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = (dto.Items ?? new List<FoodItemRequestDto>()).Select(i => new FoodItemRecord
            {
                Id = string.IsNullOrWhiteSpace(i.Id) ? Guid.NewGuid().ToString() : i.Id.Trim(),
                MealLogId = mealId,
                Name = i.Name?.Trim() ?? string.Empty,
                OriginalDetection = i.OriginalDetection?.Trim() ?? string.Empty,
                HindiOrRegionalName = i.HindiOrRegionalName?.Trim() ?? string.Empty,
                EstimatedPortion = string.IsNullOrWhiteSpace(i.EstimatedPortion) ? "1 Katori" : i.EstimatedPortion.Trim(),
                Quantity = i.Quantity,
                Grams = i.Grams,
                Calories = i.Calories,
                ProteinGrams = i.ProteinGrams,
                CarbsGrams = i.CarbsGrams,
                FatGrams = i.FatGrams,
                FiberGrams = i.FiberGrams,
                SugarGrams = i.SugarGrams,
                SodiumMg = i.SodiumMg,
                CookingMediumEstimate = string.IsNullOrWhiteSpace(i.CookingMediumEstimate) ? "Standard Home Cooking" : i.CookingMediumEstimate.Trim(),
                ConfidenceScore = i.ConfidenceScore > 0 ? i.ConfidenceScore : 0.85
            }).ToList()
        };
    }

    public static UserCorrectionDto ToDto(this UserCorrectionRecord entity) => new(
        entity.Id,
        entity.UserId,
        entity.OriginalDetectedItem,
        entity.CorrectedItemName,
        entity.HindiOrRegionalName,
        entity.EstimatedPortion,
        entity.Calories,
        entity.ProteinGrams,
        entity.CarbsGrams,
        entity.FatGrams,
        entity.MealType,
        entity.CreatedAtUtc,
        entity.FrequencyCount
    );

    public static AiDetectionFeedbackDto ToDto(this AiDetectionFeedbackRecord entity) => new(
        entity.Id,
        entity.UserId,
        entity.MealLogId,
        entity.DishName,
        entity.DetectedByModel,
        entity.ConfidenceScore,
        entity.Rating,
        entity.Remarks,
        entity.IdentifiedItemsSummary,
        entity.RetrainingTriggered,
        entity.RetrainingOutcome,
        entity.CreatedAtUtc
    );
}

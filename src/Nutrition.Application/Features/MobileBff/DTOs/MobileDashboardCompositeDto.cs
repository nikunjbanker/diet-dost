/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Features.MobileBff.DTOs;

using System.Text.Json.Serialization;
using Nutrition.Application.Features.WebBff.DTOs;

/// <summary>
/// Composite Mobile Dashboard DTO conforming strictly to Phase 1 Layer 7 Mobile BFF contracts.
/// Formatted with camelCase and null omission to minimize cellular payload size (&lt; 12 KB uncompressed, &lt; 2.5 KB compressed).
/// </summary>
public record MobileDashboardCompositeDto(
    MobileDailySummaryDto Summary,
    IReadOnlyList<MobileMealDto> TodayMeals,
    MobileAiQuotaDto Quota,
    TierFeatureFlagsDto? FeatureFlags = null,
    string? Etag = null
);

/// <summary>
/// High-level daily calorie and macronutrient progress summary for mobile HUD widgets.
/// </summary>
public record MobileDailySummaryDto(
    string Date,
    double BudgetCalories,
    double ConsumedCalories,
    double RemainingCalories,
    MobileMacroSummaryDto Macros,
    double BurnedCalories = 0.0,
    double NetCalories = 0.0
);

/// <summary>
/// Target and consumed macronutrient metrics for compact mobile dials and progress bars.
/// </summary>
public record MobileMacroSummaryDto(
    double ProteinGrams,
    double TargetProteinGrams,
    double CarbsGrams,
    double TargetCarbsGrams,
    double FatGrams,
    double TargetFatGrams,
    double FiberGrams,
    double TargetFiberGrams,
    double? SugarGrams = null,
    double? TargetSugarGrams = null
);

/// <summary>
/// Compact meal record optimized for high-density mobile list feeds.
/// </summary>
public record MobileMealDto(
    string Id,
    string MealType,
    string Name,
    double Calories,
    double Protein,
    string LoggedAt,
    string? ImageUrl = null,
    double? Carbs = null,
    double? Fat = null,
    double? Fiber = null,
    int? ItemsCount = null
);

/// <summary>
/// Mobile-tailored AI quota information for scanning limits and tier badge indicators.
/// </summary>
public record MobileAiQuotaDto(
    string Tier,
    int DailyLimit,
    int UsedToday,
    int RemainingCalls,
    DateTime ResetsAtUtc,
    int? MonthlyScanLimit = null,
    int? UsedScans = null,
    int? RemainingScans = null,
    DateTime? ResetDateUtc = null
);

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Features.WebBff.DTOs;

using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;

/// <summary>
/// Composite DTO consolidating all required data for the initial web dashboard render.
/// Eliminates 5 client-side roundtrips by hydrating user, ledger, analytics, meals, quota, and feature flags in one payload.
/// </summary>
public record WebDashboardCompositeDto(
    WebUserProfileDto User,
    DailyCalorieLedger TodayLedger,
    AnalyticsProjection Projections,
    IReadOnlyList<MealLog> RecentMeals,
    AiUsageStatsResult Quota,
    TierFeatureFlagsDto FeatureFlags
);

/// <summary>
/// User profile and target calorie/macro budget data for dashboard presentation.
/// </summary>
public record WebUserProfileDto(
    string UserId,
    string Email,
    string DisplayName,
    string Tier,
    string Role,
    BmrTdeeResult? Budget = null,
    MacroDistribution? Macros = null
);

/// <summary>
/// Dynamic tier feature gating flags for the active user session.
/// </summary>
public record TierFeatureFlagsDto(
    bool CanComparePhotos,
    bool CanExportData,
    int HistoryDayLimit,
    bool HasAdvancedAnalytics,
    bool IsAdmin
);

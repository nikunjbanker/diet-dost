/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Nutrition.Application.Features.MobileBff.DTOs;
using Nutrition.Application.Features.WebBff.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Contract snapshot tests verifying that Web BFF and Mobile BFF composite DTO schemas
/// preserve required property naming, camelCase serialization, and zero contract drift.
/// </summary>
public class BffContractSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WebDashboardCompositeDto_Serializes_With_Required_Schema_Keys()
    {
        var userDto = new WebUserProfileDto(
            UserId: "user-123",
            Email: "free@dietdost.app",
            DisplayName: "Free User",
            Tier: "Free",
            Role: "User",
            Budget: new BmrTdeeResult(1600, 2000, 1900, 500, 1500, 65.0, 22.0, "Normal", new List<string>(), new List<string>()),
            Macros: new MacroDistribution(1500, 112.5, 187.5, 33.3, 30.0, 25.0, 2000.0, 25.0)
        );

        var ledger = new DailyCalorieLedger
        {
            UserId = "user-123",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            BudgetedCalories = 2000.0,
            ConsumedCalories = 1500.0
        };

        var projections = new AnalyticsProjection("7D", 3500.0, 0.45, 1850.0, 85.0, 0, 1, false, new List<DailyTrendPoint>());
        var meals = Array.Empty<MealLog>();
        var quota = new AiUsageStatsResult(UserTier.Free, 1, 0, 1, DateTime.UtcNow.AddHours(5), 3, 10, new List<AiUsageLog>());
        var flags = new TierFeatureFlagsDto(false, false, 7, false, false);

        var composite = new WebDashboardCompositeDto(userDto, ledger, projections, meals, quota, flags);

        var json = JsonSerializer.Serialize(composite, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify root schema contracts
        Assert.True(root.TryGetProperty("user", out var userProp));
        Assert.True(root.TryGetProperty("todayLedger", out var ledgerProp));
        Assert.True(root.TryGetProperty("projections", out var projProp));
        Assert.True(root.TryGetProperty("recentMeals", out var mealsProp));
        Assert.True(root.TryGetProperty("quota", out var quotaProp));
        Assert.True(root.TryGetProperty("featureFlags", out var flagsProp));

        // Verify nested user contracts
        Assert.Equal("user-123", userProp.GetProperty("userId").GetString());
        Assert.Equal("free@dietdost.app", userProp.GetProperty("email").GetString());
        Assert.Equal("Free", userProp.GetProperty("tier").GetString());

        // Verify feature flag contracts
        Assert.False(flagsProp.GetProperty("canComparePhotos").GetBoolean());
        Assert.False(flagsProp.GetProperty("canExportData").GetBoolean());
        Assert.Equal(7, flagsProp.GetProperty("historyDayLimit").GetInt32());
    }

    [Fact]
    public void MobileDashboardCompositeDto_Serializes_With_Required_Compact_Keys()
    {
        var macros = new MobileMacroSummaryDto(
            ProteinGrams: 45.0,
            TargetProteinGrams: 90.0,
            CarbsGrams: 120.0,
            TargetCarbsGrams: 200.0,
            FatGrams: 30.0,
            TargetFatGrams: 50.0,
            FiberGrams: 18.0,
            TargetFiberGrams: 30.0,
            SugarGrams: 12.0,
            TargetSugarGrams: 25.0
        );

        var summary = new MobileDailySummaryDto(
            Date: "2026-10-10",
            BudgetCalories: 1800.0,
            ConsumedCalories: 950.0,
            RemainingCalories: 850.0,
            Macros: macros
        );

        var quota = new MobileAiQuotaDto(
            Tier: "Basic",
            DailyLimit: 7,
            UsedToday: 2,
            RemainingCalls: 5,
            ResetsAtUtc: DateTime.UtcNow.AddHours(5)
        );

        var flags = new TierFeatureFlagsDto(false, false, 7, false, false);
        var composite = new MobileDashboardCompositeDto(summary, Array.Empty<MobileMealDto>(), quota, flags, "etag-123");

        var json = JsonSerializer.Serialize(composite, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify root mobile contracts
        Assert.True(root.TryGetProperty("summary", out var summaryProp));
        Assert.True(root.TryGetProperty("todayMeals", out var mealsProp));
        Assert.True(root.TryGetProperty("quota", out var quotaProp));
        Assert.True(root.TryGetProperty("featureFlags", out var flagsProp));

        // Verify nested summary and clinical macro contracts
        Assert.Equal(1800.0, summaryProp.GetProperty("budgetCalories").GetDouble());
        Assert.Equal(950.0, summaryProp.GetProperty("consumedCalories").GetDouble());
        Assert.Equal(850.0, summaryProp.GetProperty("remainingCalories").GetDouble());

        var macrosProp = summaryProp.GetProperty("macros");
        Assert.Equal(30.0, macrosProp.GetProperty("targetFiberGrams").GetDouble());
        Assert.Equal(25.0, macrosProp.GetProperty("targetSugarGrams").GetDouble());

        // Verify quota contracts
        Assert.Equal("Basic", quotaProp.GetProperty("tier").GetString());
        Assert.Equal(7, quotaProp.GetProperty("dailyLimit").GetInt32());
        Assert.Equal(2, quotaProp.GetProperty("usedToday").GetInt32());
        Assert.Equal(5, quotaProp.GetProperty("remainingCalls").GetInt32());
    }
}

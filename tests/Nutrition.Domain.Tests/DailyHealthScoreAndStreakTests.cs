/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Xunit;

namespace Nutrition.Domain.Tests;

public class DailyHealthScoreAndStreakTests
{
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");

    [Fact]
    public void RecalculateLedger_WhenZeroMealsLogged_ReturnsZeroScoreAndAwaitingFirstMealStatus()
    {
        var ledger = new DailyCalorieLedger
        {
            BudgetedCalories = 1600,
            TargetProteinGrams = 75,
            TargetCarbsGrams = 180,
            TargetFatGrams = 45,
            TargetFiberGrams = 30
        };

        ledger.RecalculateLedger(new List<MealLog>());

        Assert.Equal(0, ledger.HealthScore);
        Assert.Equal("Awaiting First Meal 🌅", ledger.HealthScoreStatusText);
        Assert.Empty(ledger.EarnedBadges);
        Assert.Contains("breakfast", ledger.DostMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecalculateLedger_WhenStarvationCaloriesLogged_AppliesSevereCaloricDeficitPenalty()
    {
        var ledger = new DailyCalorieLedger
        {
            BudgetedCalories = 1600,
            TargetProteinGrams = 75,
            TargetCarbsGrams = 180,
            TargetFatGrams = 45,
            TargetFiberGrams = 30
        };

        // User logged only 1 black tea (15 kcal)
        var meals = new List<MealLog>
        {
            new()
            {
                TotalCalories = 15,
                TotalProteinGrams = 0.2,
                TotalCarbsGrams = 3.0,
                TotalFatGrams = 0.1,
                TotalFiberGrams = 0,
                TotalSodiumMg = 5,
                TotalSugarGrams = 2.0
            }
        };

        ledger.RecalculateLedger(meals);

        // Score must not be 100 or 85! Severely underfed day should be 0 or heavily penalized (< 30)
        Assert.True(ledger.HealthScore < 30, $"Expected penalized score for starvation calories, got {ledger.HealthScore}");
        Assert.Equal("Needs Attention ⚠️", ledger.HealthScoreStatusText);
    }

    [Fact]
    public void RecalculateLedger_WhenBalancedIndianMealsLogged_ReturnsHighHealthScoreAndOptimalStatus()
    {
        var ledger = new DailyCalorieLedger
        {
            BudgetedCalories = 1600,
            TargetProteinGrams = 75,
            TargetCarbsGrams = 180,
            TargetFatGrams = 45,
            TargetFiberGrams = 30,
            SodiumLimitMg = 2000,
            VisibleCookingOilLimitGrams = 25
        };

        // Wholesome Indian day: 1550 kcal, 72g protein, 175g carbs, 42g fat (18g visible oil), 28g fiber, 1400mg sodium, 12g sugar
        var meals = new List<MealLog>
        {
            new()
            {
                MealType = MealType.Breakfast,
                DishName = "Besan Chilla with Mint Chutney",
                TotalCalories = 350,
                TotalProteinGrams = 18,
                TotalCarbsGrams = 40,
                TotalFatGrams = 10,
                TotalFiberGrams = 7,
                TotalSodiumMg = 320,
                TotalSugarGrams = 2,
                AddedTadkaKcal = 30 // ~3.3g oil
            },
            new()
            {
                MealType = MealType.Lunch,
                DishName = "Dal Tadka, Phulkas, Bhindi Masala & Kachumber",
                TotalCalories = 620,
                TotalProteinGrams = 28,
                TotalCarbsGrams = 75,
                TotalFatGrams = 18,
                TotalFiberGrams = 12,
                TotalSodiumMg = 550,
                TotalSugarGrams = 4,
                AddedTadkaKcal = 45
            },
            new()
            {
                MealType = MealType.Dinner,
                DishName = "Paneer Bhurji with Multigrain Roti",
                TotalCalories = 580,
                TotalProteinGrams = 26,
                TotalCarbsGrams = 60,
                TotalFatGrams = 14,
                TotalFiberGrams = 9,
                TotalSodiumMg = 530,
                TotalSugarGrams = 6,
                AddedTadkaKcal = 30
            }
        };

        ledger.WaterIntakeMl = 3000;
        ledger.RecalculateLedger(meals);

        Assert.True(ledger.HealthScore >= 85, $"Expected score >= 85, got {ledger.HealthScore}");
        Assert.Equal("Optimal ICMR Adherence 🌟", ledger.HealthScoreStatusText);
        Assert.Contains("Tadka Ninja 🥷", ledger.EarnedBadges);
        Assert.Contains("Salt Sentry 🛡️", ledger.EarnedBadges);
        Assert.Contains("Protein Champion 🏆", ledger.EarnedBadges);
        Assert.Contains("Fiber Crusader 🌾", ledger.EarnedBadges);
        Assert.Contains("Deficit Sniper 🎯", ledger.EarnedBadges);
        Assert.Contains("Hydration Maharaja 💧", ledger.EarnedBadges);
    }

    [Fact]
    public void RecalculateLedger_WhenExcessiveCookingOilAndSodiumLogged_AppliesDeductions()
    {
        var ledger = new DailyCalorieLedger
        {
            BudgetedCalories = 1600,
            TargetProteinGrams = 75,
            TargetCarbsGrams = 180,
            TargetFatGrams = 45,
            TargetFiberGrams = 30,
            SodiumLimitMg = 2000,
            VisibleCookingOilLimitGrams = 25
        };

        // High oil (60g visible fat) and sodium (3500mg)
        var meals = new List<MealLog>
        {
            new()
            {
                MealType = MealType.Lunch,
                DishName = "Bhature Chole & Samosa",
                TotalCalories = 1200,
                TotalProteinGrams = 20,
                TotalCarbsGrams = 130,
                TotalFatGrams = 55,
                TotalFiberGrams = 6,
                TotalSodiumMg = 2400,
                TotalSugarGrams = 8,
                AddedTadkaKcal = 270 // ~30g added oil
            },
            new()
            {
                MealType = MealType.Dinner,
                DishName = "Pakoras & Salty Namkeen",
                TotalCalories = 800,
                TotalProteinGrams = 10,
                TotalCarbsGrams = 80,
                TotalFatGrams = 40,
                TotalFiberGrams = 3,
                TotalSodiumMg = 1500,
                TotalSugarGrams = 5,
                AddedTadkaKcal = 200
            }
        };

        ledger.RecalculateLedger(meals);

        // Exceeded budget (2000 > 1600), excessive sodium (3900 > 2000), excessive oil
        Assert.True(ledger.HealthScore < 60, $"Expected penalized score for excessive oil & sodium, got {ledger.HealthScore}");
        Assert.DoesNotContain("Tadka Ninja 🥷", ledger.EarnedBadges);
        Assert.DoesNotContain("Salt Sentry 🛡️", ledger.EarnedBadges);
    }

    [Fact]
    public void CalculateConsistencyStreak_MultiDayConsecutive_ReturnsConsecutiveStreak()
    {
        var baseUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(baseUtc, Ist));

        var meals = new List<MealLog>
        {
            // Today meal
            new() { LoggedAt = baseUtc, TotalCalories = 450 },
            // Yesterday meal
            new() { LoggedAt = baseUtc.AddDays(-1), TotalCalories = 500 },
            // 2 days ago meal
            new() { LoggedAt = baseUtc.AddDays(-2), TotalCalories = 600 }
        };

        var streak = ClinicalCalculators.CalculateConsistencyStreak(meals, Ist, today);
        Assert.Equal(3, streak);
    }

    [Fact]
    public void CalculateConsistencyStreak_WhenNotLoggedTodayButLoggedYesterday_PreservesActiveStreakGraceWindow()
    {
        var baseUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(baseUtc, Ist));

        var meals = new List<MealLog>
        {
            // Yesterday meal
            new() { LoggedAt = baseUtc.AddDays(-1), TotalCalories = 500 },
            // 2 days ago meal
            new() { LoggedAt = baseUtc.AddDays(-2), TotalCalories = 600 }
            // No meal today yet
        };

        var streak = ClinicalCalculators.CalculateConsistencyStreak(meals, Ist, today);
        // Active Grace Window: streak up through yesterday is 2 days
        Assert.Equal(2, streak);
    }

    [Fact]
    public void CalculateConsistencyStreak_WhenMissingYesterdayAndToday_ResetsStreakToZero()
    {
        var baseUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(baseUtc, Ist));

        var meals = new List<MealLog>
        {
            // 2 days ago meal only (yesterday was skipped!)
            new() { LoggedAt = baseUtc.AddDays(-2), TotalCalories = 600 }
        };

        var streak = ClinicalCalculators.CalculateConsistencyStreak(meals, Ist, today);
        Assert.Equal(0, streak);
    }

    [Fact]
    public void CalculateConsistencyStreak_SoftDeletedMeals_AreExcludedFromStreak()
    {
        var baseUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(baseUtc, Ist));

        var meals = new List<MealLog>
        {
            // Today meal is deleted!
            new() { LoggedAt = baseUtc, TotalCalories = 450, IsDeleted = true },
            // Yesterday meal is active
            new() { LoggedAt = baseUtc.AddDays(-1), TotalCalories = 500, IsDeleted = false },
            // 2 days ago was deleted
            new() { LoggedAt = baseUtc.AddDays(-2), TotalCalories = 600, IsDeleted = true }
        };

        var streak = ClinicalCalculators.CalculateConsistencyStreak(meals, Ist, today);
        // Only yesterday was valid. Today is deleted. So active grace window shows 1 day (yesterday).
        Assert.Equal(1, streak);
    }

    [Fact]
    public void CalculateConsistencyStreak_FutureDatedMeals_AreIgnoredForStreak()
    {
        var baseUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(baseUtc, Ist));

        var meals = new List<MealLog>
        {
            // Spoofed meal in future
            new() { LoggedAt = baseUtc.AddDays(5), TotalCalories = 500 },
            // Today meal
            new() { LoggedAt = baseUtc, TotalCalories = 400 }
        };

        var streak = ClinicalCalculators.CalculateConsistencyStreak(meals, Ist, today);
        Assert.Equal(1, streak);
    }
}


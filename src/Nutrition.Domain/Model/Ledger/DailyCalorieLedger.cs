/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Common;

namespace Nutrition.Domain.Model.Ledger;

public class DailyCalorieLedger : ISoftDeletable, IAggregateRoot, IConcurrencyAware
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public long ConcurrencyVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }

    public double BudgetedCalories { get; set; } = 1600.0;
    public double ConsumedCalories { get; set; } = 0.0;
    public double PendingCalories => Math.Max(0, BudgetedCalories - ConsumedCalories);

    public double TargetProteinGrams { get; set; } = 75.0;
    public double ConsumedProteinGrams { get; set; } = 0.0;

    public double TargetCarbsGrams { get; set; } = 180.0;
    public double ConsumedCarbsGrams { get; set; } = 0.0;

    public double TargetFatGrams { get; set; } = 45.0;
    public double ConsumedFatGrams { get; set; } = 0.0;

    public double TargetFiberGrams { get; set; } = 30.0;
    public double ConsumedFiberGrams { get; set; } = 0.0;

    public double TargetSugarGrams { get; set; } = 25.0; // ICMR-NIN Max 25g free sugar limit
    public double ConsumedSugarGrams { get; set; } = 0.0;

    public double SodiumLimitMg { get; set; } = 2000.0;
    public double ConsumedSodiumMg { get; set; } = 0.0;

    public double VisibleCookingOilGrams { get; set; } = 0.0;
    public double VisibleCookingOilLimitGrams { get; set; } = 25.0; // ICMR-NIN 20-25g

    public int WaterIntakeMl { get; set; } = 0;
    public int WaterTargetMl { get; set; } = 3000;

    public int ConsistencyStreakDays { get; set; } = 1;
    public int HealthScore { get; set; } = 85; // 0 - 100
    public string HealthScoreStatusText { get; set; } = "Awaiting First Meal 🌅";

    public List<string> EarnedBadges { get; set; } = new();
    public string DostMessage { get; set; } = "Namaste! Ready to nourish your body with wholesome Indian nutrition today?";

    public void RecalculateLedger(List<Model.Meal.MealLog> dayMeals)
    {
        ConsumedCalories = Math.Round(dayMeals.Sum(m => m.TotalCalories), 1);
        ConsumedProteinGrams = Math.Round(dayMeals.Sum(m => m.TotalProteinGrams), 1);
        ConsumedCarbsGrams = Math.Round(dayMeals.Sum(m => m.TotalCarbsGrams), 1);
        ConsumedFatGrams = Math.Round(dayMeals.Sum(m => m.TotalFatGrams), 1);
        ConsumedFiberGrams = Math.Round(dayMeals.Sum(m => m.TotalFiberGrams), 1);
        ConsumedSugarGrams = Math.Round(dayMeals.Sum(m => m.TotalSugarGrams), 1);
        ConsumedSodiumMg = Math.Round(dayMeals.Sum(m => m.TotalSodiumMg), 1);

        // Approximate visible cooking oil from fat and added ghee/tadka
        VisibleCookingOilGrams = Math.Round(dayMeals.Sum(m => (m.AddedGheeKcal + m.AddedTadkaKcal) / 9.0 + (m.TotalFatGrams * 0.3)), 1);

        // 1. Zero meals logged / Morning baseline state
        if (dayMeals == null || dayMeals.Count == 0)
        {
            HealthScore = 0;
            HealthScoreStatusText = "Awaiting First Meal 🌅";
            EarnedBadges.Clear();
            DostMessage = "Namaste dost! Have you had breakfast yet? Tap the camera to log your morning chai and meal! ☀️";
            return;
        }

        // 2. Severe starvation / minimal intake (< 50 kcal)
        if (ConsumedCalories < 50.0)
        {
            HealthScore = Math.Clamp((int)Math.Round((ConsumedCalories / 500.0) * 15.0), 0, 25);
            HealthScoreStatusText = "Needs Attention ⚠️";
            EarnedBadges.Clear();
            DostMessage = "Namaste dost! You've logged very few calories so far. Remember to nourish your body with adequate wholesome Indian nutrition today! ☀️";
            return;
        }

        // 3. Multi-Pillar ICMR-NIN 2024 Scoring Model (0 - 100 Points)

        // Pillar 1: Caloric Budget Adherence (Max 30 Points)
        double pCalorie;
        if (ConsumedCalories < 500.0)
        {
            pCalorie = (ConsumedCalories / 500.0) * 12.0; // starvation / extreme underfeeding
        }
        else if (ConsumedCalories < BudgetedCalories * 0.5)
        {
            pCalorie = 12.0 + ((ConsumedCalories - 500.0) / Math.Max(1.0, (BudgetedCalories * 0.5) - 500.0)) * 6.0; // 12-18 pts
        }
        else if (ConsumedCalories <= BudgetedCalories)
        {
            var deficitRatio = (BudgetedCalories - ConsumedCalories) / BudgetedCalories;
            if (deficitRatio <= 0.15)
                pCalorie = 30.0;
            else
                pCalorie = 22.0 + ((1.0 - deficitRatio) / 0.85) * 8.0;
        }
        else
        {
            var overKcal = ConsumedCalories - BudgetedCalories;
            pCalorie = Math.Max(0.0, 30.0 - (overKcal / 25.0));
        }

        // Pillar 2: Protein Adequacy (Max 25 Points)
        double pProtein;
        var proteinRatio = ConsumedProteinGrams / Math.Max(1.0, TargetProteinGrams);
        if (ConsumedCalories < 500.0)
        {
            pProtein = Math.Min(10.0, proteinRatio * 15.0);
        }
        else if (proteinRatio >= 0.85)
            pProtein = 25.0;
        else if (proteinRatio >= 0.65)
            pProtein = 18.0;
        else if (proteinRatio >= 0.45)
            pProtein = 12.0;
        else
            pProtein = 6.0;

        // Pillar 3: Clinical Moderation - Oil, Sugar, Sodium (Max 25 Points)
        var moderationScale = ConsumedCalories < 500.0 ? (ConsumedCalories / 500.0) : 1.0;

        double pOil = 10.0;
        if (VisibleCookingOilGrams > VisibleCookingOilLimitGrams)
            pOil = Math.Max(0.0, 10.0 - ((VisibleCookingOilGrams - VisibleCookingOilLimitGrams) / 5.0) * 2.0);

        double pSugar = 8.0;
        if (ConsumedSugarGrams > TargetSugarGrams)
            pSugar = Math.Max(0.0, 8.0 - ((ConsumedSugarGrams - TargetSugarGrams) / 5.0) * 2.0);

        double pSodium = 7.0;
        if (ConsumedSodiumMg > SodiumLimitMg)
            pSodium = Math.Max(0.0, 7.0 - ((ConsumedSodiumMg - SodiumLimitMg) / 300.0) * 2.0);

        var pModeration = (pOil + pSugar + pSodium) * moderationScale;

        // Pillar 4: Dietary Fiber & Gut Health (Max 10 Points)
        double pFiber;
        if (ConsumedCalories < 500.0)
            pFiber = (ConsumedFiberGrams / Math.Max(1.0, TargetFiberGrams)) * 10.0;
        else if (ConsumedFiberGrams >= 25.0)
            pFiber = 10.0;
        else if (ConsumedFiberGrams >= 15.0)
            pFiber = 6.0;
        else
            pFiber = 2.0;

        // Pillar 5: Hydration & Circadian Logging Habit (Max 10 Points)
        double pHydration = WaterIntakeMl >= 2500 ? 5.0 : (WaterIntakeMl >= 1500 ? 3.0 : (WaterIntakeMl > 0 ? 1.0 : 0.0));
        var mealTypeCount = dayMeals.Select(m => m.MealType).Distinct().Count();
        double pCadence = mealTypeCount >= 2 ? 5.0 : 2.0;
        var pHabits = pHydration + pCadence;

        var rawScore = pCalorie + pProtein + pModeration + pFiber + pHabits;
        HealthScore = Math.Clamp((int)Math.Round(rawScore), 0, 100);

        // Qualitative Classification
        if (HealthScore < 50)
            HealthScoreStatusText = "Needs Attention ⚠️";
        else if (HealthScore < 70)
            HealthScoreStatusText = "Fair Adherence ⚖️";
        else if (HealthScore < 85)
            HealthScoreStatusText = "Good Progress 🥗";
        else
            HealthScoreStatusText = "Optimal ICMR Adherence 🌟";

        // Dynamic Badges Evaluation
        EarnedBadges.Clear();
        if (ConsumedCalories >= 500.0)
        {
            if (VisibleCookingOilGrams <= VisibleCookingOilLimitGrams)
                EarnedBadges.Add("Tadka Ninja 🥷");
            if (ConsumedSodiumMg <= SodiumLimitMg)
                EarnedBadges.Add("Salt Sentry 🛡️");
            if (ConsumedProteinGrams >= TargetProteinGrams * 0.85)
                EarnedBadges.Add("Protein Champion 🏆");
            if (ConsumedFiberGrams >= 25.0 || ConsumedFiberGrams >= TargetFiberGrams * 0.80)
                EarnedBadges.Add("Fiber Crusader 🌾");
            if (Math.Abs(ConsumedCalories - BudgetedCalories) <= BudgetedCalories * 0.10)
                EarnedBadges.Add("Deficit Sniper 🎯");
            if (ConsumedSugarGrams <= TargetSugarGrams)
                EarnedBadges.Add("Sugar Guardian 🍯");
        }
        if (WaterIntakeMl >= 3000)
        {
            EarnedBadges.Add("Hydration Maharaja 💧");
        }

        // Cheerful Diet Dost Companion Micro-copy
        if (ConsumedCalories == 0)
        {
            DostMessage = "Namaste dost! Have you had breakfast yet? Tap the camera to log your morning chai and meal! ☀️";
        }
        else if (ConsumedCalories > BudgetedCalories)
        {
            DostMessage = $"Don't worry dost! A hearty Indian meal happens to the best of us. Let's take a brisk 15-minute post-meal walk and focus on hydration! 💪";
        }
        else if (PendingCalories <= 300)
        {
            DostMessage = $"Shabash! You've logged wholesome food and have {PendingCalories:F0} kcal left for a light evening snack or herbal green tea. Right on target! 🌟";
        }
        else
        {
            DostMessage = $"Wah! Looking great today. You've hit {ConsumedProteinGrams:F0}g protein with {PendingCalories:F0} kcal remaining in your daily budget! 🥗✨";
        }
    }
}

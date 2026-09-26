/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Meal;

public enum MealType
{
    Breakfast,
    Lunch,
    Snack,
    Dinner
}

public class FoodItemRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MealLogId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OriginalDetection { get; set; } = string.Empty;
    public string HindiOrRegionalName { get; set; } = string.Empty;
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
    public string CookingMediumEstimate { get; set; } = "Standard Home Cooking";
    public double ConfidenceScore { get; set; } = 0.85;
}

public class MealLog
{
    public const double ConfidenceThreshold = 0.70; // Mandatory >= 70% threshold

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public MealType MealType { get; set; } = MealType.Lunch;
    public string DishName { get; set; } = string.Empty;
    public string? PhotoUri { get; set; }
    public double OverallConfidenceScore { get; set; } = 0.85;

    public bool IsConfidencePassed => OverallConfidenceScore >= ConfidenceThreshold;

    public List<FoodItemRecord> Items { get; set; } = new();

    // 1-Tap Cooking fat modifiers
    public double AddedGheeKcal { get; set; }
    public double AddedTadkaKcal { get; set; }

    // Persisted macro totals with fallback computation
    private double? _totalCalories;
    public double TotalCalories
    {
        get => (_totalCalories.HasValue && _totalCalories.Value > 0) || Items.Count == 0
            ? (_totalCalories ?? 0.0)
            : Math.Round(Items.Sum(i => i.Calories * i.Quantity) + AddedGheeKcal + AddedTadkaKcal, 1);
        set => _totalCalories = value;
    }

    private double? _totalProteinGrams;
    public double TotalProteinGrams
    {
        get => (_totalProteinGrams.HasValue && _totalProteinGrams.Value > 0) || Items.Count == 0
            ? (_totalProteinGrams ?? 0.0)
            : Math.Round(Items.Sum(i => i.ProteinGrams * i.Quantity), 1);
        set => _totalProteinGrams = value;
    }

    private double? _totalCarbsGrams;
    public double TotalCarbsGrams
    {
        get => (_totalCarbsGrams.HasValue && _totalCarbsGrams.Value > 0) || Items.Count == 0
            ? (_totalCarbsGrams ?? 0.0)
            : Math.Round(Items.Sum(i => i.CarbsGrams * i.Quantity), 1);
        set => _totalCarbsGrams = value;
    }

    private double? _totalFatGrams;
    public double TotalFatGrams
    {
        get => (_totalFatGrams.HasValue && _totalFatGrams.Value > 0) || Items.Count == 0
            ? (_totalFatGrams ?? 0.0)
            : Math.Round(Items.Sum(i => i.FatGrams * i.Quantity) + ((AddedGheeKcal + AddedTadkaKcal) / 9.0), 1);
        set => _totalFatGrams = value;
    }

    private double? _totalFiberGrams;
    public double TotalFiberGrams
    {
        get => (_totalFiberGrams.HasValue && _totalFiberGrams.Value > 0) || Items.Count == 0
            ? (_totalFiberGrams ?? 0.0)
            : Math.Round(Items.Sum(i => i.FiberGrams * i.Quantity), 1);
        set => _totalFiberGrams = value;
    }

    private double? _totalSugarGrams;
    public double TotalSugarGrams
    {
        get => (_totalSugarGrams.HasValue && _totalSugarGrams.Value > 0) || Items.Count == 0
            ? (_totalSugarGrams ?? 0.0)
            : Math.Round(Items.Sum(i => i.SugarGrams * i.Quantity), 1);
        set => _totalSugarGrams = value;
    }

    private double? _totalSodiumMg;
    public double TotalSodiumMg
    {
        get => (_totalSodiumMg.HasValue && _totalSodiumMg.Value > 0) || Items.Count == 0
            ? (_totalSodiumMg ?? 0.0)
            : Math.Round(Items.Sum(i => i.SodiumMg * i.Quantity), 1);
        set => _totalSodiumMg = value;
    }

    public List<string> WhoComplianceFlags { get; set; } = new();
    public List<string> MedicationWarnings { get; set; } = new();
    public string? ConditionSpecificAdvice { get; set; }
    public string? DietitianAdvice { get; set; }

    public bool IsVerifiedByUser { get; set; } = false;
    public string? AiFeedbackRating { get; set; }
    public string? AiFeedbackRemarks { get; set; }

    public void RecalculateTotals()
    {
        TotalCalories = Math.Round(Items.Sum(i => i.Calories * i.Quantity) + AddedGheeKcal + AddedTadkaKcal, 1);
        TotalProteinGrams = Math.Round(Items.Sum(i => i.ProteinGrams * i.Quantity), 1);
        TotalCarbsGrams = Math.Round(Items.Sum(i => i.CarbsGrams * i.Quantity), 1);
        TotalFatGrams = Math.Round(Items.Sum(i => i.FatGrams * i.Quantity) + ((AddedGheeKcal + AddedTadkaKcal) / 9.0), 1);
        TotalFiberGrams = Math.Round(Items.Sum(i => i.FiberGrams * i.Quantity), 1);
        TotalSugarGrams = Math.Round(Items.Sum(i => i.SugarGrams * i.Quantity), 1);
        TotalSodiumMg = Math.Round(Items.Sum(i => i.SodiumMg * i.Quantity), 1);
    }
}

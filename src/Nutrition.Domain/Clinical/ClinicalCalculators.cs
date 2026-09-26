/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Domain.Clinical;

public record BmrTdeeResult(
    double Bmr,
    double Tdee,
    double AdjustedTdee,
    double DeficitCalories,
    double TargetCalories,
    double IdealBodyWeightKg,
    double Bmi,
    string BmiClassification,
    List<string> ClinicalAdjustments,
    List<string> Warnings
);

public record MacroDistribution(
    double TargetCalories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams,
    double VisibleCookingOilCeilingGrams,
    double SodiumLimitMg,
    double SugarCeilingGrams
);

public static class ClinicalCalculators
{
    // Starvation floors according to clinical safety standards
    public const double MaleStarvationFloorKcal = 1500.0;
    public const double FemaleStarvationFloorKcal = 1200.0;
    public const double MaxSafeDailyDeficitKcal = 1000.0;

    // ICMR-NIN 2024 Guidelines
    public const double CookingOilCeilingMinGrams = 20.0;
    public const double CookingOilCeilingMaxGrams = 25.0;
    public const double CerealToPulseRatioTarget = 3.0; // 3:1 minimum for amino acid complementation

    // WHO South Asian Guidelines
    public const double StandardSodiumLimitMg = 2000.0;     // < 5g salt / day
    public const double HypertensiveSodiumLimitMg = 1500.0; // < 3.75g salt / day
    public const double MaxFreeSugarLimitGrams = 25.0;      // < 5% of total calories

    /// <summary>
    /// Converts feet and inches to centimeters.
    /// 1 foot = 30.48 cm, 1 inch = 2.54 cm.
    /// </summary>
    public static double ConvertFeetInchesToCm(int feet, double inches)
    {
        var totalInches = (feet * 12.0) + inches;
        return Math.Round(totalInches * 2.54, 1);
    }

    /// <summary>
    /// Converts centimeters to feet and inches.
    /// </summary>
    public static (int Feet, double Inches) ConvertCmToFeetInches(double cm)
    {
        if (cm <= 0) return (0, 0);
        var totalInches = cm / 2.54;
        var feet = (int)Math.Floor(totalInches / 12.0);
        var inches = Math.Round(totalInches % 12.0, 1);
        if (inches >= 12.0)
        {
            feet += 1;
            inches = 0;
        }
        return (feet, inches);
    }

    /// <summary>
    /// Computes Asian-Indian BMI according to WHO South Asian specific cutoffs.
    /// </summary>
    public static (double Bmi, string Classification) ComputeWhoAsianIndianBmi(double weightKg, double heightCm)
    {
        var heightMeters = heightCm / 100.0;
        var bmi = Math.Round(weightKg / (heightMeters * heightMeters), 1);

        string classification = bmi switch
        {
            < 18.5 => "Underweight (< 18.5)",
            <= 22.9 => "Normal / Healthy South Asian (18.5 - 22.9)",
            <= 24.9 => "Overweight (23.0 - 24.9, Elevated Cardiometabolic Risk)",
            <= 29.9 => "Obese Class I (25.0 - 29.9)",
            _ => "Obese Class II (>= 30.0)"
        };

        return (bmi, classification);
    }

    /// <summary>
    /// Mifflin-St Jeor Formula calibrated for South Asian body composition.
    /// </summary>
    public static double ComputeBmr(BiologicalSex sex, double weightKg, double heightCm, int age)
    {
        return sex switch
        {
            BiologicalSex.Male => (10.0 * weightKg) + (6.25 * heightCm) - (5.0 * age) + 5.0,
            BiologicalSex.Female => (10.0 * weightKg) + (6.25 * heightCm) - (5.0 * age) - 161.0,
            _ => (10.0 * weightKg) + (6.25 * heightCm) - (5.0 * age) - 78.0
        };
    }

    /// <summary>
    /// Returns the activity multiplier used to convert BMR into estimated TDEE.
    /// </summary>
    /// <param name="level">The user's typical activity level.</param>
    /// <returns>The multiplier for the Mifflin-St Jeor estimate.</returns>
    public static double GetActivityMultiplier(ActivityLevel level)
    {
        return level switch
        {
            ActivityLevel.Sedentary => 1.20,
            ActivityLevel.Light => 1.375,
            ActivityLevel.Moderate => 1.55,
            ActivityLevel.High => 1.725,
            _ => 1.20
        };
    }

    /// <summary>
    /// Calculates full clinical caloric budget enforcing Zero Assumption Rule, ICMR-NIN, and WHO guidelines.
    /// </summary>
    public static BmrTdeeResult CalculateCaloricBudget(UserProfile profile)
    {
        profile.ValidateIntakeCompleteness();

        var bmr = ComputeBmr(profile.Sex, profile.CurrentWeightKg, profile.HeightCm, profile.Age);
        var multiplier = GetActivityMultiplier(profile.ActivityLevel);
        var baseTdee = bmr * multiplier;

        var clinicalAdjustments = new List<string>();
        var warnings = new List<string>();
        var conditions = profile.DiagnosedConditions.Select(c => c.Trim().ToLowerInvariant()).ToList();
        var meds = profile.Medications.Select(m => m.DrugName.Trim().ToLowerInvariant()).ToList();

        var adjustedTdee = baseTdee;

        // Clinical Matrix: Hypothyroidism (12% metabolic reduction)
        if (conditions.Any(c => c.Contains("hypothyroid")))
        {
            adjustedTdee *= 0.88; // 12% reduction
            clinicalAdjustments.Add("Hypothyroidism adjustment: Reduced baseline TDEE by 12% to compensate for suppressed metabolic rate.");
        }

        // Check for Levothyroxine medication timing rule
        if (meds.Any(m => m.Contains("thyronorm") || m.Contains("eltroxin") || m.Contains("levothyroxine")))
        {
            warnings.Add("Levothyroxine Timing Rule: Must be taken with plain water on an empty stomach. Prohibit tea, milk, breakfast, or calcium/iron within 60 minutes.");
        }

        // Diabetes rules
        if (conditions.Any(c => c.Contains("diabet") || c.Contains("blood sugar")))
        {
            clinicalAdjustments.Add("Diabetes clinical adjustment: Capping net carbohydrates to 35-40% of total energy. Max meal Glycemic Load (GL) < 10.");
            if (meds.Any(m => m.Contains("insulin") || m.Contains("glimepiride") || m.Contains("gliclazide")))
            {
                warnings.Add("Hypoglycemia alert: Since you take Insulin or Sulfonylureas, never skip meals or perform aggressive deficits. Keep 15g fast-acting glucose handy.");
            }
        }

        // Hypertension rules
        if (conditions.Any(c => c.Contains("hypertension") || c.Contains("high bp") || c.Contains("blood pressure")))
        {
            clinicalAdjustments.Add("Hypertension clinical adjustment: Stricter sodium ceiling enforced (< 1,500 mg/day vs standard 2,000 mg/day).");
            if (meds.Any(m => m.Contains("telmisartan") || m.Contains("ramipril") || m.Contains("amlodipine")))
            {
                warnings.Add("Medication warning: With Telmisartan/ACE inhibitors, avoid potassium-based salt substitutes (Lona/diet salt) and excessive coconut water to prevent hyperkalemia.");
            }
        }

        // Dyslipidemia rules
        if (conditions.Any(c => c.Contains("cholesterol") || c.Contains("dyslipidemia") || c.Contains("lipid")))
        {
            clinicalAdjustments.Add("Dyslipidemia adjustment: Saturated fat capped to < 7% of total calories. Minimum soluble fiber 12g/day.");
            if (meds.Any(m => m.Contains("statin") || m.Contains("atorvastatin") || m.Contains("rosuvastatin")))
            {
                warnings.Add("Statin warning: Grapefruit / Pomelo (Chakotra) strictly prohibited (inhibits CYP3A4 metabolism).");
            }
        }

        // PCOS rules
        if (conditions.Any(c => c.Contains("pcos") || c.Contains("pcod")))
        {
            clinicalAdjustments.Add("PCOS/PCOD adjustment: Elevated protein target (1.3 - 1.5g/kg IBW) with low-GI anti-inflammatory carbohydrates.");
        }

        // Gout rules
        if (conditions.Any(c => c.Contains("gout") || c.Contains("uric acid")))
        {
            clinicalAdjustments.Add("Hyperuricemia adjustment: 3.5L to 4L daily hydration mandate. Avoid acute caloric deprivation which triggers uric acid spikes.");
        }

        // Fatty liver rules
        if (conditions.Any(c => c.Contains("fatty liver") || c.Contains("nafld")))
        {
            clinicalAdjustments.Add("Fatty Liver adjustment: Zero added fructose/refined sugars. Gradual weight loss capped at 0.75 kg/week.");
        }

        // Deficit calculation
        // 0.25 kg/wk = 250 kcal/day, 0.5 kg/wk = 500 kcal/day, 0.75 kg/wk = 750 kcal/day
        var desiredPace = Math.Min(profile.DesiredPaceKgPerWeek, 0.75);
        var targetDeficit = desiredPace switch
        {
            <= 0.25 => 250.0,
            <= 0.50 => 500.0,
            _ => 750.0
        };

        // Enforce max safe deficit
        targetDeficit = Math.Min(targetDeficit, MaxSafeDailyDeficitKcal);

        var targetCalories = adjustedTdee - targetDeficit;

        // Enforce Starvation Floor
        var starvationFloor = profile.Sex == BiologicalSex.Male ? MaleStarvationFloorKcal : FemaleStarvationFloorKcal;
        if (targetCalories < starvationFloor)
        {
            targetCalories = starvationFloor;
            warnings.Add($"Starvation Safety Floor Triggered: Caloric target cannot safely fall below {starvationFloor:F0} kcal/day for {profile.Sex}. Target clamped.");
            targetDeficit = Math.Max(0, adjustedTdee - targetCalories);
        }

        // Ideal body weight for South Asian reference (BMI = 22.0)
        var heightM = profile.HeightCm / 100.0;
        var ibw = Math.Round(22.0 * (heightM * heightM), 1);
        var (bmi, bmiClass) = ComputeWhoAsianIndianBmi(profile.CurrentWeightKg, profile.HeightCm);

        return new BmrTdeeResult(
            Bmr: Math.Round(bmr, 0),
            Tdee: Math.Round(baseTdee, 0),
            AdjustedTdee: Math.Round(adjustedTdee, 0),
            DeficitCalories: Math.Round(targetDeficit, 0),
            TargetCalories: Math.Round(targetCalories, 0),
            IdealBodyWeightKg: ibw,
            Bmi: bmi,
            BmiClassification: bmiClass,
            ClinicalAdjustments: clinicalAdjustments,
            Warnings: warnings
        );
    }

    /// <summary>
    /// Distributes calories into Indian macronutrient targets with ICMR-NIN & WHO compliance.
    /// </summary>
    public static MacroDistribution CalculateMacroDistribution(UserProfile profile, BmrTdeeResult budget)
    {
        var targetKcal = budget.TargetCalories;
        var conditions = profile.DiagnosedConditions.Select(c => c.Trim().ToLowerInvariant()).ToList();

        // Protein calculation: 1.2g - 1.6g per kg of Ideal Body Weight (IBW)
        var proteinGramsPerKg = conditions.Any(c => c.Contains("pcos") || c.Contains("pcod")) ? 1.4 : 1.3;
        var proteinGrams = Math.Round(budget.IdealBodyWeightKg * proteinGramsPerKg, 1);
        var proteinKcal = proteinGrams * 4.0;

        // Fat distribution: 25% - 30% of total calories (9 kcal/g)
        var fatPercent = 0.25;
        var fatKcal = targetKcal * fatPercent;
        var fatGrams = Math.Round(fatKcal / 9.0, 1);

        // Carbohydrates: remainder of calories (4 kcal/g)
        var carbKcal = Math.Max(0, targetKcal - proteinKcal - fatKcal);
        var carbsGrams = Math.Round(carbKcal / 4.0, 1);

        // Dietary fiber: 30g minimum for South Asian metabolic health
        var fiberGrams = 30.0;

        // Sodium ceiling (Hypertension gets stricter 1500mg, otherwise 2000mg)
        var sodiumLimit = conditions.Any(c => c.Contains("hypertension") || c.Contains("high bp"))
            ? HypertensiveSodiumLimitMg
            : StandardSodiumLimitMg;

        return new MacroDistribution(
            TargetCalories: Math.Round(targetKcal, 0),
            ProteinGrams: proteinGrams,
            CarbsGrams: carbsGrams,
            FatGrams: fatGrams,
            FiberGrams: fiberGrams,
            VisibleCookingOilCeilingGrams: CookingOilCeilingMaxGrams, // 20-25g per ICMR-NIN
            SodiumLimitMg: sodiumLimit,
            SugarCeilingGrams: MaxFreeSugarLimitGrams
        );
    }
}

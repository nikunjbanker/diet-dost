/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Profile;
using Xunit;

namespace Nutrition.Domain.Tests;

public class ClinicalCalculatorsTests
{
    [Fact]
    public void ComputeBmr_ForIndianReferenceMale_ReturnsExpectedMifflinStJeorValue()
    {
        // Indian Reference Male: 65 kg, 170 cm, 35 years
        // BMR = (10 * 65) + (6.25 * 170) - (5 * 35) + 5
        //     = 650 + 1062.5 - 175 + 5 = 1542.5 kcal
        var bmr = ClinicalCalculators.ComputeBmr(BiologicalSex.Male, 65, 170, 35);
        Assert.Equal(1542.5, bmr);
    }

    [Fact]
    public void ComputeBmr_ForIndianReferenceFemale_ReturnsExpectedMifflinStJeorValue()
    {
        // Indian Reference Female: 55 kg, 160 cm, 35 years
        // BMR = (10 * 55) + (6.25 * 160) - (5 * 35) - 161
        //     = 550 + 1000 - 175 - 161 = 1214.0 kcal
        var bmr = ClinicalCalculators.ComputeBmr(BiologicalSex.Female, 55, 160, 35);
        Assert.Equal(1214.0, bmr);
    }

    [Fact]
    public void ComputeWhoAsianIndianBmi_FollowsSouthAsianCutoffs()
    {
        // 170cm, 65kg -> BMI = 65 / (1.7^2) = 22.49 -> Normal
        var (normalBmi, normalClass) = ClinicalCalculators.ComputeWhoAsianIndianBmi(65, 170);
        Assert.Equal(22.5, normalBmi);
        Assert.Contains("Normal", normalClass);

        // 170cm, 70kg -> BMI = 70 / (1.7^2) = 24.22 -> Overweight (above 23.0 for South Asians)
        var (overweightBmi, overweightClass) = ClinicalCalculators.ComputeWhoAsianIndianBmi(70, 170);
        Assert.Equal(24.2, overweightBmi);
        Assert.Contains("Overweight", overweightClass);

        // 170cm, 80kg -> BMI = 80 / (1.7^2) = 27.68 -> Obese Class I (25.0 - 29.9)
        var (obeseBmi, obeseClass) = ClinicalCalculators.ComputeWhoAsianIndianBmi(80, 170);
        Assert.Equal(27.7, obeseBmi);
        Assert.Contains("Obese Class I", obeseClass);
    }

    [Fact]
    public void CalculateCaloricBudget_EnforcesStarvationFloor_ForFemale()
    {
        // Female with low weight/height that would calculate < 1200 kcal deficit
        var profile = new UserProfile
        {
            Name = "Priya Sharma",
            Sex = BiologicalSex.Female,
            Age = 40,
            HeightCm = 150,
            CurrentWeightKg = 50,
            TargetWeightKg = 45,
            DesiredPaceKgPerWeek = 0.75, // 750 kcal aggressive deficit
            ActivityLevel = ActivityLevel.Sedentary,
            DiagnosedConditions = new() { "None" }
        };

        var result = ClinicalCalculators.CalculateCaloricBudget(profile);

        // Target calories must NEVER drop below 1200 kcal for females
        Assert.True(result.TargetCalories >= ClinicalCalculators.FemaleStarvationFloorKcal);
        Assert.Contains(result.Warnings, w => w.Contains("Starvation Safety Floor Triggered"));
    }

    [Fact]
    public void CalculateCaloricBudget_Hypothyroidism_ReducesTdeeBy12Percent()
    {
        var normalProfile = new UserProfile
        {
            Name = "Rahul Verma",
            Sex = BiologicalSex.Male,
            Age = 35,
            HeightCm = 175,
            CurrentWeightKg = 85,
            TargetWeightKg = 75,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Sedentary,
            DiagnosedConditions = new() { "None" }
        };

        var hypoProfile = new UserProfile
        {
            Name = "Rahul Verma",
            Sex = BiologicalSex.Male,
            Age = 35,
            HeightCm = 175,
            CurrentWeightKg = 85,
            TargetWeightKg = 75,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Sedentary,
            DiagnosedConditions = new() { "Hypothyroidism" },
            Medications = new()
            {
                new MedicationEntry { DrugName = "Thyronorm 50mcg", Dosage = "50mcg", Frequency = "Morning" }
            }
        };

        var normalResult = ClinicalCalculators.CalculateCaloricBudget(normalProfile);
        var hypoResult = ClinicalCalculators.CalculateCaloricBudget(hypoProfile);

        // Adjusted TDEE should be ~88% of base TDEE
        Assert.True(hypoResult.AdjustedTdee < normalResult.AdjustedTdee);
        Assert.Contains(hypoResult.ClinicalAdjustments, a => a.Contains("Hypothyroidism"));
        Assert.Contains(hypoResult.Warnings, w => w.Contains("Levothyroxine Timing Rule"));
    }

    [Fact]
    public void ZeroAssumptionRule_ThrowsOnMissingMetrics()
    {
        var incompleteProfile = new UserProfile
        {
            Name = "", // Missing name
            Age = 0    // Missing age
        };

        Assert.Throws<InvalidOperationException>(() => incompleteProfile.ValidateIntakeCompleteness());
    }

    [Fact]
    public void CalculateMacroDistribution_FollowsIcmrCookingOilCeiling_AndSodiumLimits()
    {
        var profile = new UserProfile
        {
            Name = "Anjali Patel",
            Sex = BiologicalSex.Female,
            Age = 32,
            HeightCm = 162,
            CurrentWeightKg = 68,
            TargetWeightKg = 58,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Moderate,
            DiagnosedConditions = new() { "Hypertension" }
        };

        var budget = ClinicalCalculators.CalculateCaloricBudget(profile);
        var macros = ClinicalCalculators.CalculateMacroDistribution(profile, budget);

        // Visible cooking oil ceiling should be 20-25g per ICMR-NIN
        Assert.Equal(25.0, macros.VisibleCookingOilCeilingGrams);
        // Hypertension sodium limit should be 1500mg (stricter than WHO 2000mg)
        Assert.Equal(1500.0, macros.SodiumLimitMg);
    }

    [Fact]
    public void HeightConversion_FeetInchesAndCm_ConvertsAccurately()
    {
        // 5 ft 9 in = (5 * 30.48) + (9 * 2.54) = 152.4 + 22.86 = 175.26 -> 175.3 cm
        var cm = ClinicalCalculators.ConvertFeetInchesToCm(5, 9);
        Assert.Equal(175.3, cm);

        var (feet, inches) = ClinicalCalculators.ConvertCmToFeetInches(175.3);
        Assert.Equal(5, feet);
        Assert.Equal(9.0, inches);

        // 5 ft 0 in = 152.4 cm
        var cm5ft = ClinicalCalculators.ConvertFeetInchesToCm(5, 0);
        Assert.Equal(152.4, cm5ft);

        var (f5, i0) = ClinicalCalculators.ConvertCmToFeetInches(152.4);
        Assert.Equal(5, f5);
        Assert.Equal(0.0, i0);
    }

    [Fact]
    public void IsIntakeComplete_ForNewlyCreatedSkeletonProfile_ReturnsFalse()
    {
        var profile = new UserProfile
        {
            Id = "usr_skeleton_test",
            Name = "Demo3",
            Timezone = "Asia/Kolkata",
            IsOnboarded = true
        };

        Assert.False(profile.IsIntakeComplete());
    }

    [Fact]
    public void IsIntakeComplete_ForCompleteProfile_ReturnsTrue()
    {
        var profile = new UserProfile
        {
            Id = "usr_complete_test",
            Name = "Aarav Sharma",
            Age = 32,
            Sex = BiologicalSex.Male,
            HeightCm = 175,
            CurrentWeightKg = 82,
            TargetWeightKg = 72,
            DesiredPaceKgPerWeek = 0.50,
            ActivityLevel = ActivityLevel.Sedentary,
            Timezone = "Asia/Kolkata",
            IsOnboarded = true
        };

        Assert.True(profile.IsIntakeComplete());
    }

    [Fact]
    public void IsIntakeComplete_WhenNotOnboarded_ReturnsFalseEvenWithPopulatedMetrics()
    {
        var profile = new UserProfile
        {
            Id = "usr_not_onboarded_test",
            Name = "demo1@demo.com",
            Age = 30,
            HeightCm = 170,
            CurrentWeightKg = 70,
            TargetWeightKg = 65,
            DesiredPaceKgPerWeek = 0.50,
            Timezone = "Asia/Kolkata",
            IsOnboarded = false
        };

        Assert.False(profile.IsIntakeComplete());
    }

    [Theory]
    [InlineData(0, 175, 82, 72, 0.5)]   // Invalid Age
    [InlineData(32, 50, 82, 72, 0.5)]   // Height too low (<80cm)
    [InlineData(32, 175, 20, 72, 0.5)]  // Weight too low (<25kg)
    [InlineData(32, 175, 82, 20, 0.5)]  // Target weight too low (<25kg)
    [InlineData(32, 175, 82, 72, 1.0)]  // Unsafe pace (>0.75kg/wk)
    public void IsIntakeComplete_WithInvalidMetrics_ReturnsFalse(int age, double height, double currentWeight, double targetWeight, double pace)
    {
        var profile = new UserProfile
        {
            Id = "usr_invalid_test",
            Name = "Priya",
            Age = age,
            HeightCm = height,
            CurrentWeightKg = currentWeight,
            TargetWeightKg = targetWeight,
            DesiredPaceKgPerWeek = pace,
            Timezone = "Asia/Kolkata",
            IsOnboarded = true
        };

        Assert.False(profile.IsIntakeComplete());
    }
}

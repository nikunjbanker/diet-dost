/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Mcp.Models;

public record NutritionSummaryDto(
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams,
    double SugarGrams,
    double SodiumMg
);

public record LoggedItemDto(
    string Name,
    string Portion,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    double Fiber
);

public record LoggedMealDto(
    string Id,
    string MealType,
    DateTime LoggedAtUtc,
    bool IsVerifiedByUser,
    string DishName,
    List<LoggedItemDto> Items
);

public record ViewMealLogsResult(
    string User,
    string Period,
    string? SelectedDate,
    int TotalMealsCount,
    NutritionSummaryDto NutritionTotals,
    List<LoggedMealDto> Meals
);

public record FoodPortionEstimateResult(
    string Dish,
    string Portion,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams
);

public record ProposedMealDto(
    string MealType,
    double TotalCalories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    List<DraftItem> Items
);

public record MealElicitationRequiredResult(
    string Status,
    string Message,
    string ApprovalToken,
    ProposedMealDto ProposedMeal,
    string Instruction
);

public record MealConfirmedResult(
    string Status,
    string Message,
    string MealId,
    double TotalCalories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams
);

public record MealCancelledResult(
    string Status,
    string Message
);

public record McpErrorResult(
    string Status,
    string Error
);

public record DailyLedgerMacrosDto(
    double ProteinGrams,
    double TargetProteinGrams,
    double CarbsGrams,
    double TargetCarbsGrams,
    double FatGrams,
    double TargetFatGrams,
    double FiberGrams,
    double TargetFiberGrams,
    double SugarGrams,
    double TargetSugarGrams,
    double SodiumMg,
    double SodiumLimitMg
);

public record DailyLedgerResult(
    string User,
    string Date,
    double CaloricBudget,
    double CaloriesConsumed,
    double CalorieBalance,
    bool IsDeficit,
    DailyLedgerMacrosDto Macros,
    int HealthScore,
    int ConsistencyStreakDays
);

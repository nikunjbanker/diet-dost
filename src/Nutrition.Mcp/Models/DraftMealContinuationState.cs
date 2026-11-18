/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Mcp.Models;

public record DraftItem(
    string Name,
    string Portion,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    double Fiber
);

public record DraftMealContinuationState(
    string UserId,
    string MealType,
    string Description,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    double TotalFiber,
    DateTime ExpiresAtUtc,
    string Nonce,
    List<DraftItem> Items
);

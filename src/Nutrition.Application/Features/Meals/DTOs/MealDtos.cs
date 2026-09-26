/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Agents;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.DTOs;

public record MealAnalysisResultDto(
    bool ConfidenceGated,
    double ConfidenceScore,
    string? DetectedByModel,
    string? PhotoUrl,
    string? Message,
    string? Advice,
    bool RequiresRetake,
    IndianMealAnalysisResult? Analysis
);

public record ConfirmMealResultDto(
    MealLog Meal,
    DailyCalorieLedger DailyLedger,
    List<string> LearnedNotes,
    string? LearnedMessage,
    string Message
);

public record UpdateMealResultDto(
    MealLog Meal,
    DailyCalorieLedger DailyLedger,
    string Message
);

public record DeleteMealResultDto(
    bool Success,
    string Message
);

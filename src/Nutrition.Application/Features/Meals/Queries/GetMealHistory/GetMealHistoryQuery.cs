/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.GetMealHistory;

public record GetMealHistoryQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper,
    string Period,
    string? Date,
    string? MealType
) : IQuery<Result<MealHistoryResponseDto>>;

public record MealHistorySummaryDto(
    double TotalCalories,
    double TotalProteinGrams,
    double TotalCarbsGrams,
    double TotalFatGrams,
    double TotalFiberGrams,
    double TotalSugarGrams,
    double TotalSodiumMg
);

public record MealHistoryResponseDto(
    string UserId,
    string Period,
    string? SelectedDate,
    string? MealType,
    int TotalMealsCount,
    MealHistorySummaryDto Summary,
    List<MealLog> Meals
);

public class GetMealHistoryQueryHandler : IQueryHandler<GetMealHistoryQuery, Result<MealHistoryResponseDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public GetMealHistoryQueryHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<MealHistoryResponseDto>> HandleAsync(GetMealHistoryQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<MealHistoryResponseDto>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var d))
        {
            parsedDate = d;
        }

        MealType? parsedMealType = null;
        if (!string.IsNullOrWhiteSpace(request.MealType) && Enum.TryParse<MealType>(request.MealType, true, out var mt))
        {
            parsedMealType = mt;
        }

        var meals = await _dietitianService.GetMealHistoryAsync(effectiveUserId, request.Period, parsedDate, parsedMealType, ct);

        double totalCalories = Math.Round(meals.Sum(m => m.TotalCalories), 1);
        double totalProtein = Math.Round(meals.Sum(m => m.TotalProteinGrams), 1);
        double totalCarbs = Math.Round(meals.Sum(m => m.TotalCarbsGrams), 1);
        double totalFat = Math.Round(meals.Sum(m => m.TotalFatGrams), 1);
        double totalFiber = Math.Round(meals.Sum(m => m.TotalFiberGrams), 1);
        double totalSugar = Math.Round(meals.Sum(m => m.TotalSugarGrams), 1);
        double totalSodium = Math.Round(meals.Sum(m => m.TotalSodiumMg), 1);

        var summary = new MealHistorySummaryDto(
            totalCalories,
            totalProtein,
            totalCarbs,
            totalFat,
            totalFiber,
            totalSugar,
            totalSodium
        );

        return Result<MealHistoryResponseDto>.Success(new MealHistoryResponseDto(
            effectiveUserId,
            request.Period,
            parsedDate?.ToString("yyyy-MM-dd"),
            parsedMealType?.ToString(),
            meals.Count,
            summary,
            meals
        ));
    }
}

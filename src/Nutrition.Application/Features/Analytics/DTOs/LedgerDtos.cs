/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Ledger;

namespace Nutrition.Application.Features.Analytics.DTOs;

public sealed record DailyCalorieLedgerDto(
    string Id,
    string UserId,
    DateOnly Date,
    double BudgetedCalories,
    double ConsumedCalories,
    double PendingCalories,
    double TargetProteinGrams,
    double ConsumedProteinGrams,
    double TargetCarbsGrams,
    double ConsumedCarbsGrams,
    double TargetFatGrams,
    double ConsumedFatGrams,
    double TargetFiberGrams,
    double ConsumedFiberGrams,
    double TargetSugarGrams,
    double ConsumedSugarGrams,
    double SodiumLimitMg,
    double ConsumedSodiumMg,
    double VisibleCookingOilGrams,
    double VisibleCookingOilLimitGrams,
    int WaterIntakeMl,
    int WaterTargetMl,
    int ConsistencyStreakDays,
    int HealthScore,
    string HealthScoreStatusText,
    List<string> EarnedBadges,
    string DostMessage
);

public static class LedgerDtoMapper
{
    public static DailyCalorieLedgerDto ToDto(this DailyCalorieLedger entity) => new(
        Id: entity.Id,
        UserId: entity.UserId,
        Date: entity.Date,
        BudgetedCalories: entity.BudgetedCalories,
        ConsumedCalories: entity.ConsumedCalories,
        PendingCalories: entity.PendingCalories,
        TargetProteinGrams: entity.TargetProteinGrams,
        ConsumedProteinGrams: entity.ConsumedProteinGrams,
        TargetCarbsGrams: entity.TargetCarbsGrams,
        ConsumedCarbsGrams: entity.ConsumedCarbsGrams,
        TargetFatGrams: entity.TargetFatGrams,
        ConsumedFatGrams: entity.ConsumedFatGrams,
        TargetFiberGrams: entity.TargetFiberGrams,
        ConsumedFiberGrams: entity.ConsumedFiberGrams,
        TargetSugarGrams: entity.TargetSugarGrams,
        ConsumedSugarGrams: entity.ConsumedSugarGrams,
        SodiumLimitMg: entity.SodiumLimitMg,
        ConsumedSodiumMg: entity.ConsumedSodiumMg,
        VisibleCookingOilGrams: entity.VisibleCookingOilGrams,
        VisibleCookingOilLimitGrams: entity.VisibleCookingOilLimitGrams,
        WaterIntakeMl: entity.WaterIntakeMl,
        WaterTargetMl: entity.WaterTargetMl,
        ConsistencyStreakDays: entity.ConsistencyStreakDays,
        HealthScore: entity.HealthScore,
        HealthScoreStatusText: entity.HealthScoreStatusText,
        EarnedBadges: entity.EarnedBadges.ToList(),
        DostMessage: entity.DostMessage
    );
}

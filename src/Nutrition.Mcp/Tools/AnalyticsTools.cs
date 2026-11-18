/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Mcp.Models;

namespace Nutrition.Mcp.Tools;

/// <summary>
/// Model Context Protocol Tools exposing Daily Nutritional Ledger, Caloric Budget, and Macro Distribution.
/// Dispatches directly to Native CQRS handlers via IDispatcher.
/// </summary>
[McpServerToolType]
public class AnalyticsTools
{
    [McpServerTool]
    [Description("Retrieves the user's daily nutritional ledger showing target calories vs consumed, macro split (protein, carbs, fat), remaining balance, and adherence.")]
    public async Task<object> ViewDailyLedger(
        IDispatcher dispatcher,
        [Description("Target date in 'YYYY-MM-DD' format (defaults to current date if omitted)")] string? date = null,
        ClaimsPrincipal? user = null,
        CancellationToken ct = default)
    {
        var userId = ResolveUserId(user);
        var isAdmin = user != null && (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"));

        var query = new GetDailyLedgerQuery(
            CurrentUserId: userId,
            TargetUserId: null,
            IsAdminOrSuper: isAdmin,
            Date: date
        );

        var result = await dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded || result.Data == null)
        {
            return new McpErrorResult("Error", result.Error ?? "Unable to retrieve daily ledger.");
        }

        var ledger = result.Data;
        return new DailyLedgerResult(
            User: userId,
            Date: ledger.Date.ToString("yyyy-MM-dd"),
            CaloricBudget: ledger.BudgetedCalories,
            CaloriesConsumed: ledger.ConsumedCalories,
            CalorieBalance: ledger.PendingCalories,
            IsDeficit: ledger.ConsumedCalories <= ledger.BudgetedCalories,
            Macros: new DailyLedgerMacrosDto(
                ProteinGrams: ledger.ConsumedProteinGrams,
                TargetProteinGrams: ledger.TargetProteinGrams,
                CarbsGrams: ledger.ConsumedCarbsGrams,
                TargetCarbsGrams: ledger.TargetCarbsGrams,
                FatGrams: ledger.ConsumedFatGrams,
                TargetFatGrams: ledger.TargetFatGrams,
                FiberGrams: ledger.ConsumedFiberGrams,
                TargetFiberGrams: ledger.TargetFiberGrams,
                SugarGrams: ledger.ConsumedSugarGrams,
                TargetSugarGrams: ledger.TargetSugarGrams,
                SodiumMg: ledger.ConsumedSodiumMg,
                SodiumLimitMg: ledger.SodiumLimitMg
            ),
            HealthScore: ledger.HealthScore,
            ConsistencyStreakDays: ledger.ConsistencyStreakDays
        );
    }

    private static string ResolveUserId(ClaimsPrincipal? user)
    {
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? user?.FindFirst("uid")?.Value
            ?? "free@dietdost.app";
    }
}

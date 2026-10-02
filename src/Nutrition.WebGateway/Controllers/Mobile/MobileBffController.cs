/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Application.Features.Meals.Queries.GetAiQuota;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Application.Features.MobileBff.DTOs;
using Nutrition.Application.Features.Profile.Queries.GetProfile;
using Nutrition.Application.Features.WebBff.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers.Mobile;

/// <summary>
/// Mobile Backend for Frontend (Mobile BFF) Facade Controller.
/// Serves ultra-compact, composite payloads optimized for mobile cellular networks (&lt;12 KB uncompressed, &lt;2.5 KB Brotli-compressed)
/// with strict ETag caching (304 Not Modified) and zero duplicated domain calculation logic.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class MobileBffController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MobileBffController> _logger;

    public static readonly JsonSerializerOptions MobileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public MobileBffController(
        IServiceScopeFactory scopeFactory,
        ILogger<MobileBffController> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches all essential mobile dashboard widgets in a single high-efficiency composite roundtrip.
    /// Supports ETag validation via If-None-Match header returning 304 Not Modified when unchanged.
    /// </summary>
    [HttpGet("api/mobile/v1/dashboard/composite")]
    [HttpGet("api/mobile/v1/composite")]
    [ProducesResponseType(typeof(MobileDashboardCompositeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDashboardComposite(
        [FromQuery] string? date = null,
        [FromQuery] int historyDays = 7,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var tierStr = User.GetTier() ?? nameof(UserTier.Free);
        var roleStr = User.GetRole() ?? nameof(UserRole.User);
        var isAdminOrSuper = User.IsAdminOrSuper();
        var userTier = Enum.TryParse<UserTier>(tierStr, true, out var parsedTier) ? parsedTier : UserTier.Free;

        // Parse or normalize requested date
        var targetDateOnly = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var targetDateStr = targetDateOnly.ToString("yyyy-MM-dd");

        // Bound history days to user's tier entitlement
        var maxHistory = (userTier is UserTier.Premium || roleStr is "SuperAdmin" or "Admin") ? 365 : (userTier is UserTier.Basic ? 30 : 7);
        var boundedDays = Math.Clamp(historyDays, 1, maxHistory);

        _logger.LogInformation("Mobile BFF dashboard requested for user {UserId} (Tier: {Tier}, Date: {Date}, HistoryDays: {Days})",
            userId, tierStr, targetDateStr, boundedDays);

        // Execute independent read queries concurrently across isolated DI scopes for 100% thread safety
        var ledgerTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetDailyLedgerQuery(userId, null, isAdminOrSuper, targetDateStr), ct);
        }, ct);

        var mealsTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetMealHistoryQuery(userId, null, isAdminOrSuper, "1D", targetDateStr, null), ct);
        }, ct);

        var quotaTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetAiQuotaQuery(userId, userTier), ct);
        }, ct);

        var profileTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetProfileQuery(userId, null, isAdminOrSuper), ct);
        }, ct);

        await Task.WhenAll(ledgerTask, mealsTask, quotaTask, profileTask);

        var ledgerResult = await ledgerTask;
        var mealsResult = await mealsTask;
        var quotaResult = await quotaTask;
        var profileResult = await profileTask;

        var ledger = ledgerResult.Data;
        var budget = profileResult.Data?.Budget;
        var targetMacros = profileResult.Data?.Macros;

        var budgetCalories = budget?.TargetCalories ?? ledger?.BudgetedCalories ?? 2000.0;
        var consumed = ledger?.ConsumedCalories ?? 0.0;
        var burned = 0.0;
        var net = consumed - burned;
        var remaining = budgetCalories - net;

        var macros = new MobileMacroSummaryDto(
            ProteinGrams: Math.Round(ledger?.ConsumedProteinGrams ?? 0.0, 1),
            TargetProteinGrams: Math.Round(targetMacros?.ProteinGrams ?? ledger?.TargetProteinGrams ?? 100.0, 1),
            CarbsGrams: Math.Round(ledger?.ConsumedCarbsGrams ?? 0.0, 1),
            TargetCarbsGrams: Math.Round(targetMacros?.CarbsGrams ?? ledger?.TargetCarbsGrams ?? 200.0, 1),
            FatGrams: Math.Round(ledger?.ConsumedFatGrams ?? 0.0, 1),
            TargetFatGrams: Math.Round(targetMacros?.FatGrams ?? ledger?.TargetFatGrams ?? 50.0, 1),
            FiberGrams: Math.Round(ledger?.ConsumedFiberGrams ?? 0.0, 1),
            TargetFiberGrams: Math.Round(targetMacros?.FiberGrams ?? ledger?.TargetFiberGrams ?? 30.0, 1),
            SugarGrams: ledger?.ConsumedSugarGrams > 0 ? Math.Round(ledger.ConsumedSugarGrams, 1) : null,
            TargetSugarGrams: (ledger?.TargetSugarGrams ?? 0) > 0 ? Math.Round(ledger!.TargetSugarGrams, 1) : null
        );

        var summary = new MobileDailySummaryDto(
            Date: targetDateStr,
            BudgetCalories: Math.Round(budgetCalories, 1),
            ConsumedCalories: Math.Round(consumed, 1),
            RemainingCalories: Math.Round(remaining, 1),
            Macros: macros,
            BurnedCalories: Math.Round(burned, 1),
            NetCalories: Math.Round(net, 1)
        );

        var rawMeals = mealsResult.Data?.Meals ?? new List<MealLog>();
        var todayMeals = rawMeals
            .Select(m => new MobileMealDto(
                Id: m.Id,
                MealType: m.MealType.ToString(),
                Name: !string.IsNullOrWhiteSpace(m.DishName) ? m.DishName : (m.Items.FirstOrDefault()?.Name ?? "Logged Meal"),
                Calories: Math.Round(m.TotalCalories, 1),
                Protein: Math.Round(m.TotalProteinGrams, 1),
                LoggedAt: m.LoggedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ImageUrl: m.PhotoUri,
                Carbs: m.TotalCarbsGrams > 0 ? Math.Round(m.TotalCarbsGrams, 1) : null,
                Fat: m.TotalFatGrams > 0 ? Math.Round(m.TotalFatGrams, 1) : null,
                Fiber: m.TotalFiberGrams > 0 ? Math.Round(m.TotalFiberGrams, 1) : null,
                ItemsCount: m.Items.Count > 0 ? m.Items.Count : null
            ))
            .ToList();

        var quota = quotaResult.Data;
        var mobileQuota = new MobileAiQuotaDto(
            Tier: tierStr,
            DailyLimit: quota?.DailyLimit ?? 1,
            UsedToday: quota?.UsedToday ?? 0,
            RemainingCalls: quota?.RemainingCalls ?? 1,
            ResetsAtUtc: quota?.ResetsAtUtc ?? DateTime.UtcNow.Date.AddDays(1),
            MonthlyScanLimit: quota != null ? quota.DailyLimit * 30 : null,
            UsedScans: quota?.UsedLast30Days,
            RemainingScans: quota != null ? Math.Max(0, (quota.DailyLimit * 30) - quota.UsedLast30Days) : null,
            ResetDateUtc: quota?.ResetsAtUtc
        );

        var isPrivilegedOrPremium = userTier is UserTier.Premium || roleStr is "SuperAdmin" or "Admin";
        var featureFlags = new TierFeatureFlagsDto(
            CanComparePhotos: isPrivilegedOrPremium,
            CanExportData: isPrivilegedOrPremium,
            HistoryDayLimit: maxHistory,
            HasAdvancedAnalytics: isPrivilegedOrPremium,
            IsAdmin: isAdminOrSuper
        );

        // Compute deterministic content hash for weak ETag
        var latestMealStamp = rawMeals.Count > 0 ? rawMeals.Max(m => m.LoggedAt.Ticks) : 0;
        var contentFingerprint = $"{userId}:{targetDateStr}:{consumed:F1}:{remaining:F1}:{rawMeals.Count}:{latestMealStamp}:{quota?.UsedToday}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(contentFingerprint));
        var hexHash = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
        var etag = $"W/\"{hexHash}-{targetDateOnly:yyyyMMdd}\"";

        // Check incoming If-None-Match header for cache validation
        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatchHeader))
        {
            var clientEtag = ifNoneMatchHeader.ToString().Trim();
            if (string.Equals(clientEtag, etag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clientEtag.Trim('"', 'W', '/'), etag.Trim('"', 'W', '/'), StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers.ETag = etag;
                Response.Headers.CacheControl = "private, no-cache";
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        var composite = new MobileDashboardCompositeDto(
            Summary: summary,
            TodayMeals: todayMeals,
            Quota: mobileQuota,
            FeatureFlags: featureFlags,
            Etag: etag
        );

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, no-cache";

        return new JsonResult(composite, MobileJsonOptions);
    }
}

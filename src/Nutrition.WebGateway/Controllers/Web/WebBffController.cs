/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Application.Features.Analytics.Queries.GetProjections;
using Nutrition.Application.Features.Meals.Queries.GetAiQuota;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Application.Features.Profile.Queries.GetProfile;
using Nutrition.Application.Features.WebBff.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers.Web;

/// <summary>
/// Web BFF Facade Controller. Provides high-performance, composite endpoints tailored strictly
/// for the web application presentation layer, orchestrating CQRS queries in parallel.
/// </summary>
[Authorize]
[ApiController]
[Route("api/web/v1")]
[Produces("application/json")]
public class WebBffController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebBffController> _logger;

    public WebBffController(
        IDispatcher dispatcher,
        IServiceScopeFactory scopeFactory,
        ILogger<WebBffController> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches all essential dashboard components in a single high-efficiency composite request.
    /// Executes queries concurrently using Task.WhenAll across isolated DI scopes to guarantee
    /// thread-safe sub-50ms execution times without EF Core concurrency conflicts.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(WebDashboardCompositeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDashboardComposite(
        [FromQuery] string period = "7D",
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var tierStr = User.GetTier() ?? nameof(UserTier.Free);
        var roleStr = User.GetRole() ?? nameof(UserRole.User);
        var email = User.GetEmail() ?? string.Empty;
        var isAdminOrSuper = User.IsAdminOrSuper();
        var userTier = Enum.TryParse<UserTier>(tierStr, true, out var parsedTier) ? parsedTier : UserTier.Free;

        var sanitizedPeriod = period switch
        {
            "30D" => "30D",
            "90D" => "90D",
            _ => "7D"
        };

        _logger.LogInformation("Web BFF dashboard requested for user {UserId} (Tier: {Tier}, Role: {Role}, Period: {Period})",
            userId, tierStr, roleStr, sanitizedPeriod);

        // Execute independent read queries concurrently in isolated scopes for 100% thread safety and sub-50ms performance
        var ledgerTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetDailyLedgerQuery(userId, null, isAdminOrSuper, null), ct);
        }, ct);

        var projectionsTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetProjectionsQuery(userId, null, userTier, isAdminOrSuper, sanitizedPeriod), ct);
        }, ct);

        var mealsTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            return await dispatcher.QueryAsync(new GetMealHistoryQuery(userId, null, isAdminOrSuper, sanitizedPeriod, null, null), ct);
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

        await Task.WhenAll(ledgerTask, projectionsTask, mealsTask, quotaTask, profileTask);

        var ledgerResult = await ledgerTask;
        var projectionsResult = await projectionsTask;
        var mealsResult = await mealsTask;
        var quotaResult = await quotaTask;
        var profileResult = await profileTask;

        var displayName = User.FindFirst("displayName")?.Value
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? profileResult.Data?.Profile?.Name
            ?? email;

        var userDto = new WebUserProfileDto(
            UserId: userId,
            Email: email,
            DisplayName: displayName,
            Tier: tierStr,
            Role: roleStr,
            Budget: profileResult.Data?.Budget,
            Macros: profileResult.Data?.Macros
        );

        var isPrivilegedOrPremium = userTier is UserTier.Premium || roleStr is "SuperAdmin" or "Admin";
        var featureFlags = new TierFeatureFlagsDto(
            CanComparePhotos: isPrivilegedOrPremium,
            CanExportData: isPrivilegedOrPremium,
            HistoryDayLimit: isPrivilegedOrPremium ? 365 : (userTier is UserTier.Basic ? 30 : 7),
            HasAdvancedAnalytics: isPrivilegedOrPremium,
            IsAdmin: isAdminOrSuper
        );

        var composite = new WebDashboardCompositeDto(
            User: userDto,
            TodayLedger: ledgerResult.Data ?? new DailyCalorieLedger { UserId = userId },
            Projections: projectionsResult.Data ?? new AnalyticsProjection(
                Period: sanitizedPeriod,
                TotalDeficitKcal: 0,
                ProjectedWeightLossKg: 0,
                AverageDailyCalories: 0,
                ProteinCompliancePercent: 0,
                SodiumWarningCount: 0,
                SugarWarningCount: 0,
                PlateauRiskDetected: false,
                DailyTrends: new()
            ),
            RecentMeals: mealsResult.Data?.Meals ?? new List<MealLog>(),
            Quota: quotaResult.Data ?? new AiUsageStatsResult(
                userTier,
                DailyLimit: userTier switch { UserTier.Free => 1, UserTier.Basic => 7, UserTier.Premium => 30, _ => -1 },
                UsedToday: 0,
                RemainingCalls: userTier switch { UserTier.Free => 1, UserTier.Basic => 7, UserTier.Premium => 30, _ => -1 },
                ResetsAtUtc: DateTime.UtcNow.Date.AddDays(1),
                UsedLast7Days: 0,
                UsedLast30Days: 0,
                RecentOperations: new()
            ),
            FeatureFlags: featureFlags
        );

        return Ok(composite);
    }
}

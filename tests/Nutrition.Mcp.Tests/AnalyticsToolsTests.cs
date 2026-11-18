/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Moq;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Mcp.Models;
using Nutrition.Mcp.Tools;
using Xunit;

namespace Nutrition.Mcp.Tests;

public class AnalyticsToolsTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly AnalyticsTools _analyticsTools;
    private readonly ClaimsPrincipal _user;

    public AnalyticsToolsTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _analyticsTools = new AnalyticsTools();

        _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "ledger-user@dietdost.app"),
            new Claim("tier", "Premium")
        }, "TestAuth"));
    }

    [Fact]
    public async Task ViewDailyLedger_DispatchesQuery_AndReturnsBudgetMetrics()
    {
        var ledger = new DailyCalorieLedger
        {
            UserId = "ledger-user@dietdost.app",
            Date = new DateOnly(2026, 11, 18),
            BudgetedCalories = 1800,
            ConsumedCalories = 1450,
            TargetProteinGrams = 80,
            ConsumedProteinGrams = 75,
            TargetCarbsGrams = 200,
            ConsumedCarbsGrams = 180,
            TargetFatGrams = 50,
            ConsumedFatGrams = 42,
            TargetFiberGrams = 30,
            ConsumedFiberGrams = 28,
            HealthScore = 92
        };

        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetDailyLedgerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DailyCalorieLedger>.Success(ledger));

        var result = await _analyticsTools.ViewDailyLedger(_dispatcherMock.Object, date: "2026-11-18", user: _user);
        Assert.NotNull(result);

        var typed = Assert.IsType<DailyLedgerResult>(result);
        Assert.Equal("ledger-user@dietdost.app", typed.User);
        Assert.Equal(1800.0, typed.CaloricBudget);
        Assert.Equal(1450.0, typed.CaloriesConsumed);
        Assert.Equal(350.0, typed.CalorieBalance);
        Assert.True(typed.IsDeficit);
        Assert.Equal(92, typed.HealthScore);
    }
}

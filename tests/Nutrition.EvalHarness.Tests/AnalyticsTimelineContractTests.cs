/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.IO;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Contract tests verifying that the Analytics Card timeline navigation and client presentation components
/// strictly support Daily (1D), Weekly (7D), Monthly (30D), and Quarterly (90D) intervals, while completely
/// excluding the retired Yearly (365D) view to prevent data consolidation bloat.
/// </summary>
public class AnalyticsTimelineContractTests
{
    private readonly string _wwwroot;

    public AnalyticsTimelineContractTests()
    {
        _wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/wwwroot"));
    }

    [Fact]
    public void AnalyticsCard_ContainsDailyWeeklyMonthlyQuarterly_AndExcludesYearlyTab()
    {
        var analyticsCardPath = Path.Combine(_wwwroot, "partials", "analytics-card.html");
        Assert.True(File.Exists(analyticsCardPath), $"analytics-card.html must exist at {analyticsCardPath}");
        var content = File.ReadAllText(analyticsCardPath);

        // Assert 1D, 7D, 30D, 90D tabs exist
        Assert.Contains("data-period=\"1D\"", content);
        Assert.Contains("data-period=\"7D\"", content);
        Assert.Contains("data-period=\"30D\"", content);
        Assert.Contains("data-period=\"90D\"", content);

        // Assert 365D Yearly tab is completely removed to prevent data consolidation bloat
        Assert.DoesNotContain("data-period=\"365D\"", content);
        Assert.DoesNotContain("Yearly", content);
    }

    [Fact]
    public void AnalyticsChartJs_TitlesMap_ContainsDailyToday_AndExcludesYearly()
    {
        var chartJsPath = Path.Combine(_wwwroot, "js", "ui", "analytics-chart.js");
        Assert.True(File.Exists(chartJsPath), $"analytics-chart.js must exist at {chartJsPath}");
        var content = File.ReadAllText(chartJsPath);

        Assert.Contains("'1D': 'Daily (Today)'", content);
        Assert.Contains("'7D': '7-Day'", content);
        Assert.Contains("'30D': '30-Day'", content);
        Assert.Contains("'90D': 'Quarterly (90D)'", content);
        Assert.DoesNotContain("365D", content);
    }

    [Fact]
    public void MealDiaryViewJs_PeriodLabels_ContainsDailyToday_AndExcludesYearly()
    {
        var diaryViewJsPath = Path.Combine(_wwwroot, "js", "ui", "MealDiaryView.js");
        Assert.True(File.Exists(diaryViewJsPath), $"MealDiaryView.js must exist at {diaryViewJsPath}");
        var content = File.ReadAllText(diaryViewJsPath);

        Assert.Contains("'1D': \"Today's\"", content);
        Assert.Contains("'7D': '7-Day'", content);
        Assert.Contains("'30D': '30-Day'", content);
        Assert.Contains("'90D': 'Quarterly (90D)'", content);
        Assert.DoesNotContain("365D", content);
    }
}

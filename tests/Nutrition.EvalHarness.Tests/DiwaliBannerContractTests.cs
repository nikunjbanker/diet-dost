/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DiwaliBannerContractTests
{
    private readonly string _wwwroot;

    public DiwaliBannerContractTests()
    {
        _wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/wwwroot"));
    }

    [Fact]
    public void IndexHtml_IncludesDiwaliCelebrationBanner_AndRemovesDussehra()
    {
        var indexPath = Path.Combine(_wwwroot, "index.html");
        Assert.True(File.Exists(indexPath), $"index.html must exist at {indexPath}");
        var content = File.ReadAllText(indexPath);

        Assert.Contains(@"data-include=""partials/diwali-celebration-banner.html""", content);
        Assert.DoesNotContain(@"data-include=""partials/dussehra-launch-banner.html""", content);
    }

    [Fact]
    public void Partials_DiwaliCelebrationBanner_Exists_AndDussehraIsRemoved()
    {
        var diwaliPartialPath = Path.Combine(_wwwroot, "partials", "diwali-celebration-banner.html");
        var dussehraPartialPath = Path.Combine(_wwwroot, "partials", "dussehra-launch-banner.html");

        Assert.True(File.Exists(diwaliPartialPath), $"diwali-celebration-banner.html must exist at {diwaliPartialPath}");
        Assert.False(File.Exists(dussehraPartialPath), $"dussehra-launch-banner.html should be removed at {dussehraPartialPath}");

        var html = File.ReadAllText(diwaliPartialPath);

        // Core IDs and Semantic Landmark
        Assert.Contains(@"id=""diwali-celebration-banner""", html);
        Assert.Contains(@"id=""diwali-ticker-bar""", html);
        Assert.Contains(@"id=""diwali-carousel-wrapper""", html);

        // 4 Concept Slides
        Assert.Contains(@"id=""slide-diwali-0""", html);
        Assert.Contains(@"id=""slide-diwali-1""", html);
        Assert.Contains(@"id=""slide-diwali-2""", html);
        Assert.Contains(@"id=""slide-diwali-3""", html);

        // Interactive Panch-Deep 5-Day Diya Lighting System
        Assert.Contains(@"id=""diwali-panch-deep""", html);
        Assert.Contains(@"diwali-diya-btn", html);

        // Smart Mithai & Farsan Calibrator
        Assert.Contains(@"id=""mithai-dynamic-card""", html);
        Assert.Contains(@"mithai-chip", html);

        // Gujarati New Year & Saal Mubarak Cultural Elements
        Assert.Contains(@"Saal Mubarak", html);
        Assert.Contains(@"સાલ મુબારક", html);
        Assert.Contains(@"Bestu Varas", html);

        // Creator Personal Branding
        Assert.Contains(@"Nikunj Banker", html);
    }

    [Fact]
    public void StylesCss_ContainsDiwaliBannerStyles_AndRemovesLegacyDussehra()
    {
        var stylesPath = Path.Combine(_wwwroot, "styles.css");
        Assert.True(File.Exists(stylesPath), $"styles.css must exist at {stylesPath}");
        var styles = File.ReadAllText(stylesPath);

        Assert.Contains(".diwali-banner-card", styles);
        Assert.Contains("@keyframes diyaFlicker", styles);
        Assert.Contains(".diwali-diya-btn", styles);
        Assert.Contains(".mithai-chip", styles);
        Assert.DoesNotContain(".dussehra-banner-card", styles);
    }

    [Fact]
    public void MainJs_WiresDiwaliBannerController()
    {
        var mainJsPath = Path.Combine(_wwwroot, "js", "main.js");
        Assert.True(File.Exists(mainJsPath), $"main.js must exist at {mainJsPath}");
        var js = File.ReadAllText(mainJsPath);

        Assert.Contains("setupDiwaliBanner", js);
        Assert.Contains("diet_dost_diwali_diyas_lit", js);
        Assert.Contains("diet_dost_diwali_minimized", js);
        Assert.DoesNotContain("setupDussehraBanner", js);
    }
}

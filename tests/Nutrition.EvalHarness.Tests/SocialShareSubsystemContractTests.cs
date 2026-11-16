/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

using System.Text.RegularExpressions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public partial class SocialShareSubsystemContractTests
{
    private readonly string _wwwroot;

    public SocialShareSubsystemContractTests()
    {
        _wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/wwwroot"));
    }

    [GeneratedRegex(@"data-include=[""']partials/share-modal\.html[""']")]
    private static partial Regex ShareModalIncludeRegex();

    [GeneratedRegex(@"\.share-modal-overlay\s*\{")]
    private static partial Regex ShareModalOverlayCssRegex();

    [GeneratedRegex(@"\.share-studio-layout\s*\{")]
    private static partial Regex ShareStudioLayoutCssRegex();

    [Fact]
    public void IndexHtml_IncludesShareModalPartial()
    {
        var indexPath = Path.Combine(_wwwroot, "index.html");
        Assert.True(File.Exists(indexPath), $"index.html must exist at {indexPath}");
        var html = File.ReadAllText(indexPath);

        Assert.Matches(ShareModalIncludeRegex(), html);
    }

    [Fact]
    public void ShareModalHtml_ExistsAndDefinesRequiredControls()
    {
        var modalPath = Path.Combine(_wwwroot, "partials", "share-modal.html");
        Assert.True(File.Exists(modalPath), $"share-modal.html must exist at {modalPath}");
        var content = File.ReadAllText(modalPath);

        Assert.Contains("id=\"share-modal\"", content);
        Assert.Contains("id=\"share-card-img\"", content);
        Assert.Contains("id=\"btn-aspect-square\"", content);
        Assert.Contains("id=\"btn-aspect-story\"", content);
        Assert.Contains("id=\"btn-copy-share-image\"", content);
        Assert.Contains("id=\"btn-share-whatsapp-web\"", content);
        Assert.Contains("id=\"btn-download-share-card\"", content);
        Assert.Contains("id=\"btn-copy-share-text\"", content);
        Assert.Contains("id=\"btn-close-share-modal\"", content);
    }

    [Fact]
    public void ReviewModalHtml_HasShareMealButton()
    {
        var reviewPath = Path.Combine(_wwwroot, "partials", "review-modal.html");
        Assert.True(File.Exists(reviewPath), $"review-modal.html must exist at {reviewPath}");
        var content = File.ReadAllText(reviewPath);

        Assert.Contains("btn-share-review-meal", content);
    }

    [Fact]
    public void HeroHudHtml_HasShareDailyHudButton()
    {
        var hudPath = Path.Combine(_wwwroot, "partials", "hero-hud.html");
        Assert.True(File.Exists(hudPath), $"hero-hud.html must exist at {hudPath}");
        var content = File.ReadAllText(hudPath);

        Assert.Contains("btn-share-daily-hud", content);
    }

    [Fact]
    public void ShareServiceJs_ExistsAndImplementsCoreMethods()
    {
        var servicePath = Path.Combine(_wwwroot, "js", "services", "share-service.js");
        Assert.True(File.Exists(servicePath), $"share-service.js must exist at {servicePath}");
        var content = File.ReadAllText(servicePath);

        Assert.Contains("class ShareService", content);
        Assert.Contains("shareMeal", content);
        Assert.Contains("shareDaily", content);
        Assert.Contains("copyImageToClipboard", content);
        Assert.Contains("copyTextToClipboard", content);
        Assert.Contains("generateMealShareText", content);
        Assert.Contains("generateDailyShareText", content);
        // Assert web app link inclusion per user feedback
        Assert.Contains("https://dietdost.app", content);
    }

    [Fact]
    public void ShareCardRendererJs_ExistsAndImplementsCanvasRendering()
    {
        var rendererPath = Path.Combine(_wwwroot, "js", "ui", "ShareCardRenderer.js");
        Assert.True(File.Exists(rendererPath), $"ShareCardRenderer.js must exist at {rendererPath}");
        var content = File.ReadAllText(rendererPath);

        Assert.Contains("class ShareCardRenderer", content);
        Assert.Contains("renderMealCard", content);
        Assert.Contains("renderDailyCard", content);
        Assert.Contains("1080", content);
        Assert.Contains("dietdost.app", content);
    }

    [Fact]
    public void ShareModalJs_ExistsAndIntegratesWithModalStack()
    {
        var modalJsPath = Path.Combine(_wwwroot, "js", "ui", "share-modal.js");
        Assert.True(File.Exists(modalJsPath), $"share-modal.js must exist at {modalJsPath}");
        var content = File.ReadAllText(modalJsPath);

        Assert.Contains("class ShareModalController", content);
        Assert.Contains("modalStack.push", content);
        Assert.Contains("modalStack.pop", content);
        Assert.Contains("open", content);
        Assert.Contains("close", content);
    }

    [Fact]
    public void MealDiaryViewJs_HasShareMealAction()
    {
        var diaryPath = Path.Combine(_wwwroot, "js", "ui", "MealDiaryView.js");
        Assert.True(File.Exists(diaryPath), $"MealDiaryView.js must exist at {diaryPath}");
        var content = File.ReadAllText(diaryPath);

        Assert.Contains("btn-share-meal", content);
    }

    [Fact]
    public void StylesCss_DefinesShareModalAndStudioRules()
    {
        var stylesPath = Path.Combine(_wwwroot, "styles.css");
        Assert.True(File.Exists(stylesPath), $"styles.css must exist at {stylesPath}");
        var content = File.ReadAllText(stylesPath);

        Assert.Matches(ShareModalOverlayCssRegex(), content);
        Assert.Matches(ShareStudioLayoutCssRegex(), content);
    }
}


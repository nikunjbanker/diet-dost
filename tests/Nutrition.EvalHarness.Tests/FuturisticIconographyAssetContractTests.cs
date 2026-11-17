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

public partial class FuturisticIconographyAssetContractTests
{
    private readonly string _repoRoot;
    private readonly string _wwwroot;
    private readonly string _iconsDir;

    private static readonly string[] RequiredIconNames =
    [
        "share",
        "edit",
        "delete",
        "close",
        "view-cards",
        "view-grid",
        "export-excel",
        "aspect-square",
        "aspect-story",
        "clipboard",
        "x-twitter",
        "whatsapp",
        "download",
        "copy-text",
        "facebook",
        "email",
        "system-share",
        "meal-breakfast",
        "meal-lunch",
        "meal-snack",
        "meal-dinner"
    ];

    public FuturisticIconographyAssetContractTests()
    {
        _repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        _wwwroot = Path.Combine(_repoRoot, "src", "Nutrition.WebGateway", "wwwroot");
        _iconsDir = Path.Combine(_wwwroot, "assets", "icons");
    }

    [GeneratedRegex(@"data-include=[""']assets/icons/sprite\.svg[""']")]
    private static partial Regex SpriteIncludeRegex();

    [Fact]
    public void SpriteSheet_ExistsAndDefinesAllRequiredSymbols()
    {
        var spritePath = Path.Combine(_iconsDir, "sprite.svg");
        Assert.True(File.Exists(spritePath), $"sprite.svg must exist at {spritePath}");
        var content = File.ReadAllText(spritePath);

        foreach (var iconName in RequiredIconNames)
        {
            var symbolId = $"id=\"icon-{iconName}\"";
            Assert.Contains(symbolId, content);
        }
    }

    [Fact]
    public void StandaloneSvgFiles_AllExistAndAreValidSvg()
    {
        Assert.True(Directory.Exists(_iconsDir), $"icons directory must exist at {_iconsDir}");

        foreach (var iconName in RequiredIconNames)
        {
            var filePath = Path.Combine(_iconsDir, $"{iconName}.svg");
            Assert.True(File.Exists(filePath), $"Standalone icon {iconName}.svg must exist at {filePath}");

            var svgContent = File.ReadAllText(filePath);
            Assert.StartsWith("<svg", svgContent.TrimStart());
            Assert.EndsWith("</svg>", svgContent.TrimEnd());
        }
    }

    [Fact]
    public void IndexHtml_PreloadsOrIncludesSpriteSheet()
    {
        var indexPath = Path.Combine(_wwwroot, "index.html");
        Assert.True(File.Exists(indexPath), $"index.html must exist at {indexPath}");
        var content = File.ReadAllText(indexPath);

        Assert.Matches(SpriteIncludeRegex(), content);
    }

    [Fact]
    public void ShareModal_UsesSvgSpriteSymbolsInsteadOfRawEmojis()
    {
        var modalPath = Path.Combine(_wwwroot, "partials", "share-modal.html");
        Assert.True(File.Exists(modalPath), $"share-modal.html must exist at {modalPath}");
        var content = File.ReadAllText(modalPath);

        Assert.Contains("assets/icons/sprite.svg#icon-close", content);
        Assert.Contains("assets/icons/sprite.svg#icon-aspect-square", content);
        Assert.Contains("assets/icons/sprite.svg#icon-aspect-story", content);
        Assert.Contains("assets/icons/sprite.svg#icon-clipboard", content);
        Assert.Contains("assets/icons/sprite.svg#icon-whatsapp", content);
        Assert.Contains("assets/icons/sprite.svg#icon-download", content);
        Assert.Contains("assets/icons/sprite.svg#icon-copy-text", content);
        Assert.Contains("assets/icons/sprite.svg#icon-facebook", content);
        Assert.Contains("assets/icons/sprite.svg#icon-x-twitter", content);
        Assert.Contains("assets/icons/sprite.svg#icon-email", content);

        // Assert obsolete Twitter bird emoji is not present
        Assert.DoesNotContain("🐦", content);
    }

    [Fact]
    public void ReviewModal_UsesSvgSpriteSymbolsForCloseAndMealTypes()
    {
        var modalPath = Path.Combine(_wwwroot, "partials", "review-modal.html");
        Assert.True(File.Exists(modalPath), $"review-modal.html must exist at {modalPath}");
        var content = File.ReadAllText(modalPath);

        Assert.Contains("assets/icons/sprite.svg#icon-close", content);
        Assert.Contains("assets/icons/sprite.svg#icon-share", content);
        Assert.Contains("assets/icons/sprite.svg#icon-meal-breakfast", content);
        Assert.Contains("assets/icons/sprite.svg#icon-meal-lunch", content);
        Assert.Contains("assets/icons/sprite.svg#icon-meal-snack", content);
        Assert.Contains("assets/icons/sprite.svg#icon-meal-dinner", content);

        // Assert meal timing pills do not use old sun/moon raw emojis
        Assert.DoesNotContain("🌅 Breakfast", content);
        Assert.DoesNotContain("☀️ Lunch", content);
        Assert.DoesNotContain("☕ Snack", content);
        Assert.DoesNotContain("🌙 Dinner", content);
    }

    [Fact]
    public void AnalyticsCard_UsesSvgSpriteSymbolsForTogglesAndExport()
    {
        var cardPath = Path.Combine(_wwwroot, "partials", "analytics-card.html");
        Assert.True(File.Exists(cardPath), $"analytics-card.html must exist at {cardPath}");
        var content = File.ReadAllText(cardPath);

        Assert.Contains("assets/icons/sprite.svg#icon-view-cards", content);
        Assert.Contains("assets/icons/sprite.svg#icon-view-grid", content);
        Assert.Contains("assets/icons/sprite.svg#icon-export-excel", content);
        Assert.Contains("assets/icons/sprite.svg#icon-close", content);
    }

    [Fact]
    public void MealDiaryViewJs_UsesSvgSpriteSymbolsForActionsAndMealTypes()
    {
        var diaryPath = Path.Combine(_wwwroot, "js", "ui", "MealDiaryView.js");
        Assert.True(File.Exists(diaryPath), $"MealDiaryView.js must exist at {diaryPath}");
        var content = File.ReadAllText(diaryPath);

        Assert.Contains("assets/icons/sprite.svg#icon-share", content);
        Assert.Contains("assets/icons/sprite.svg#icon-edit", content);
        Assert.Contains("assets/icons/sprite.svg#icon-delete", content);
        Assert.Contains("icon-meal-breakfast", content);
        Assert.Contains("icon-meal-lunch", content);
        Assert.Contains("icon-meal-snack", content);
        Assert.Contains("icon-meal-dinner", content);

        // Ensure legacy action emojis in button labels are not present
        Assert.DoesNotContain("✏️ Edit", content);
        Assert.DoesNotContain("🗑️ Delete", content);
    }

    [Fact]
    public void StylesCss_DefinesFuturisticIconClasses()
    {
        var stylesPath = Path.Combine(_wwwroot, "styles.css");
        Assert.True(File.Exists(stylesPath), $"styles.css must exist at {stylesPath}");
        var content = File.ReadAllText(stylesPath);

        Assert.Contains(".btn-modal-close", content);
        Assert.Contains(".icon-modal-close", content);
        Assert.Contains(".icon-action-edit", content);
        Assert.Contains(".icon-action-delete", content);
        Assert.Contains(".icon-view-cards", content);
        Assert.Contains(".icon-view-grid", content);
        Assert.Contains(".icon-export-excel", content);
        Assert.Contains(".icon-meal-type", content);
    }

    [Fact]
    public void AgentRulesAndDesignMd_DocumentCentralizedIconographyStandard()
    {
        var rulesPath = Path.Combine(_repoRoot, ".agents", "rules", "ui-ux-design-system.md");
        var skillPath = Path.Combine(_repoRoot, ".agents", "skills", "design-system-enforcer", "SKILL.md");
        var designPath = Path.Combine(_repoRoot, "DESIGN.md");

        Assert.True(File.Exists(rulesPath), $"{rulesPath} must exist");
        Assert.True(File.Exists(skillPath), $"{skillPath} must exist");
        Assert.True(File.Exists(designPath), $"{designPath} must exist");

        var rulesContent = File.ReadAllText(rulesPath);
        var skillContent = File.ReadAllText(skillPath);
        var designContent = File.ReadAllText(designPath);

        Assert.Contains("assets/icons/sprite.svg", rulesContent);
        Assert.Contains("assets/icons/sprite.svg", skillContent);
        Assert.Contains("assets/icons/sprite.svg", designContent);
    }
}


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
        "meal-dinner",
        "users",
        "activity",
        "settings",
        "telemetry",
        "shield-check",
        "signout",
        "plus",
        "refresh",
        "lock",
        "unlock",
        "bell",
        "tier-free",
        "tier-basic",
        "tier-premium",
        "tier-superadmin",
        "bolt",
        "key",
        "user",
        "camera",
        "image",
        "mic",
        "scan",
        "search",
        "face",
        "body",
        "folder",
        "check",
        "fire",
        "muscle",
        "grain",
        "lipid",
        "leaf",
        "cube",
        "shaker",
        "stethoscope",
        "clock",
        "info",
        "target",
        "thumb-up",
        "thumb-down",
        "bot",
        "printer",
        "desktop",
        "phone"
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
        Assert.Contains("assets/icons/sprite.svg#icon-desktop", content);
        Assert.DoesNotContain("💻", content);
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
        Assert.Contains("assets/icons/sprite.svg#icon-fire", content);
        Assert.Contains("assets/icons/sprite.svg#icon-muscle", content);
        Assert.Contains("assets/icons/sprite.svg#icon-grain", content);
        Assert.Contains("assets/icons/sprite.svg#icon-lipid", content);
        Assert.Contains("assets/icons/sprite.svg#icon-leaf", content);
        Assert.Contains("assets/icons/sprite.svg#icon-cube", content);
        Assert.Contains("assets/icons/sprite.svg#icon-camera", content);
        Assert.Contains("assets/icons/sprite.svg#icon-search", content);
        Assert.Contains("assets/icons/sprite.svg#icon-clock", content);
        Assert.Contains("assets/icons/sprite.svg#icon-stethoscope", content);
        Assert.Contains("assets/icons/sprite.svg#icon-target", content);
        Assert.Contains("assets/icons/sprite.svg#icon-thumb-up", content);
        Assert.Contains("assets/icons/sprite.svg#icon-thumb-down", content);
        Assert.Contains("assets/icons/sprite.svg#icon-bot", content);

        Assert.DoesNotContain("🌅 Breakfast", content);
        Assert.DoesNotContain("☀️ Lunch", content);
        Assert.DoesNotContain("☕ Snack", content);
        Assert.DoesNotContain("🌙 Dinner", content);
        Assert.DoesNotContain("🔥 Calories", content);
        Assert.DoesNotContain("💪 Protein", content);
        Assert.DoesNotContain("🌾 Carbs", content);
        Assert.DoesNotContain("🥑 Fat", content);
        Assert.DoesNotContain("🥗 Fiber", content);
        Assert.DoesNotContain("🍬 Sugar", content);
        Assert.DoesNotContain("📸 Plate Photo", content);
        Assert.DoesNotContain("🕒", content);
        Assert.DoesNotContain("🩺", content);
        Assert.DoesNotContain("🎯", content);
        Assert.DoesNotContain("👍", content);
        Assert.DoesNotContain("👎", content);
        Assert.DoesNotContain("🤖", content);
    }

    [Fact]
    public void AdminModal_UsesSvgSpriteSymbolsForTabsAndActions()
    {
        var modalPath = Path.Combine(_wwwroot, "partials", "admin-modal.html");
        Assert.True(File.Exists(modalPath), $"admin-modal.html must exist at {modalPath}");
        var content = File.ReadAllText(modalPath);

        Assert.Contains("assets/icons/sprite.svg#icon-users", content);
        Assert.Contains("assets/icons/sprite.svg#icon-activity", content);
        Assert.Contains("assets/icons/sprite.svg#icon-settings", content);
        Assert.Contains("assets/icons/sprite.svg#icon-telemetry", content);
        Assert.Contains("assets/icons/sprite.svg#icon-delete", content);
        Assert.Contains("assets/icons/sprite.svg#icon-plus", content);
        Assert.Contains("assets/icons/sprite.svg#icon-refresh", content);
        Assert.Contains("assets/icons/sprite.svg#icon-edit", content);

        Assert.DoesNotContain("👥 User Directory", content);
        Assert.DoesNotContain("🟢 Active Users", content);
        Assert.DoesNotContain("⚙️ Tier Configurations", content);
        Assert.DoesNotContain("📡 AI Usage Telemetry", content);
        Assert.DoesNotContain("🗑️ Deleted Audit", content);
        Assert.DoesNotContain("➕ Create User", content);
        Assert.DoesNotContain("🔄 Refresh", content);
        Assert.DoesNotContain("✏️ Update User", content);
    }

    [Fact]
    public void HeaderHtml_UsesSvgSpriteSymbols()
    {
        var headerPath = Path.Combine(_wwwroot, "partials", "header.html");
        Assert.True(File.Exists(headerPath), $"header.html must exist at {headerPath}");
        var content = File.ReadAllText(headerPath);

        Assert.Contains("assets/icons/sprite.svg#icon-body", content);
        Assert.Contains("assets/icons/sprite.svg#icon-target", content);
        Assert.Contains("assets/icons/sprite.svg#icon-settings", content);
        Assert.Contains("assets/icons/sprite.svg#icon-bell", content);
        Assert.Contains("assets/icons/sprite.svg#icon-user", content);
        Assert.Contains("assets/icons/sprite.svg#icon-bolt", content);
        Assert.Contains("assets/icons/sprite.svg#icon-telemetry", content);
        Assert.Contains("assets/icons/sprite.svg#icon-shield-check", content);
        Assert.Contains("assets/icons/sprite.svg#icon-signout", content);

        Assert.DoesNotContain("🧍 Visual Progress", content);
        Assert.DoesNotContain("📐 Calculation Transparency", content);
        Assert.DoesNotContain("⚙️ Clinical Intake", content);
        Assert.DoesNotContain("🔔", content);
        Assert.DoesNotContain("👤", content);
        Assert.DoesNotContain("⚡ Premium", content);
        Assert.DoesNotContain("📊 AI Quota", content);
        Assert.DoesNotContain("👑 Admin Governance", content);
        Assert.DoesNotContain("🚪 Sign Out", content);
    }

    [Fact]
    public void MealLoggerHtml_UsesSvgSpriteSymbols()
    {
        var loggerPath = Path.Combine(_wwwroot, "partials", "meal-logger.html");
        Assert.True(File.Exists(loggerPath), $"meal-logger.html must exist at {loggerPath}");
        var content = File.ReadAllText(loggerPath);

        Assert.Contains("assets/icons/sprite.svg#icon-clock", content);
        Assert.Contains("assets/icons/sprite.svg#icon-camera", content);
        Assert.Contains("assets/icons/sprite.svg#icon-mic", content);
        Assert.Contains("assets/icons/sprite.svg#icon-image", content);
        Assert.Contains("assets/icons/sprite.svg#icon-scan", content);
        Assert.Contains("assets/icons/sprite.svg#icon-tier-free", content);

        Assert.DoesNotContain("📸 AI Camera", content);
        Assert.DoesNotContain("🎙️ Voice & Smart Search", content);
        Assert.DoesNotContain("🍲", content);
        Assert.DoesNotContain("📸 Take Photo", content);
        Assert.DoesNotContain("🖼️ Choose from Gallery", content);
        Assert.DoesNotContain("✨ Try Sample Thali", content);
    }

    [Fact]
    public void AuthGateHtml_UsesSvgSpriteSymbols()
    {
        var authPath = Path.Combine(_wwwroot, "partials", "auth-gate.html");
        Assert.True(File.Exists(authPath), $"auth-gate.html must exist at {authPath}");
        var content = File.ReadAllText(authPath);

        Assert.Contains("assets/icons/sprite.svg#icon-desktop", content);
        Assert.Contains("assets/icons/sprite.svg#icon-bolt", content);
        Assert.Contains("assets/icons/sprite.svg#icon-key", content);
        Assert.Contains("assets/icons/sprite.svg#icon-tier-free", content);
        Assert.Contains("assets/icons/sprite.svg#icon-tier-basic", content);
        Assert.Contains("assets/icons/sprite.svg#icon-tier-premium", content);
        Assert.Contains("assets/icons/sprite.svg#icon-shield-check", content);
        Assert.Contains("assets/icons/sprite.svg#icon-phone", content);
        Assert.Contains("assets/icons/sprite.svg#icon-clock", content);
        Assert.Contains("assets/icons/sprite.svg#icon-lock", content);
        Assert.Contains("assets/icons/sprite.svg#icon-folder", content);

        Assert.DoesNotContain("💻", content);
        Assert.DoesNotContain("⚡ QR Login", content);
        Assert.DoesNotContain("🔑 Phrase", content);
        Assert.DoesNotContain("⭐ Free Tier", content);
        Assert.DoesNotContain("⭐ Basic Tier", content);
        Assert.DoesNotContain("👑 Premium Tier", content);
        Assert.DoesNotContain("🛡️ Admin Portal", content);
        Assert.DoesNotContain("📱", content);
        Assert.DoesNotContain("🔒", content);
    }

    [Fact]
    public void ProgressModalHtml_UsesSvgSpriteSymbols()
    {
        var progressPath = Path.Combine(_wwwroot, "partials", "progress-modal.html");
        Assert.True(File.Exists(progressPath), $"progress-modal.html must exist at {progressPath}");
        var content = File.ReadAllText(progressPath);

        Assert.Contains("assets/icons/sprite.svg#icon-body", content);
        Assert.Contains("assets/icons/sprite.svg#icon-face", content);
        Assert.Contains("assets/icons/sprite.svg#icon-image", content);
        Assert.Contains("assets/icons/sprite.svg#icon-plus", content);
        Assert.Contains("assets/icons/sprite.svg#icon-camera", content);
        Assert.Contains("assets/icons/sprite.svg#icon-folder", content);
        Assert.Contains("assets/icons/sprite.svg#icon-check", content);

        Assert.DoesNotContain("👤 Face Photo", content);
        Assert.DoesNotContain("🧍 Full Body", content);
        Assert.DoesNotContain("📸 Take Photo", content);
        Assert.DoesNotContain("📁 Choose File", content);
        Assert.DoesNotContain("Save Progress Check-In 🎉", content);
    }

    [Fact]
    public void DeleteMealModalHtml_UsesSvgSpriteSymbols()
    {
        var deletePath = Path.Combine(_wwwroot, "partials", "delete-meal-modal.html");
        Assert.True(File.Exists(deletePath), $"delete-meal-modal.html must exist at {deletePath}");
        var content = File.ReadAllText(deletePath);

        Assert.Contains("assets/icons/sprite.svg#icon-close", content);
        Assert.Contains("assets/icons/sprite.svg#icon-delete", content);
        Assert.Contains("assets/icons/sprite.svg#icon-meal-lunch", content);
        Assert.Contains("assets/icons/sprite.svg#icon-fire", content);
        Assert.Contains("assets/icons/sprite.svg#icon-muscle", content);
        Assert.Contains("assets/icons/sprite.svg#icon-grain", content);
        Assert.Contains("assets/icons/sprite.svg#icon-lipid", content);
        Assert.Contains("assets/icons/sprite.svg#icon-leaf", content);
        Assert.Contains("assets/icons/sprite.svg#icon-cube", content);
        Assert.Contains("assets/icons/sprite.svg#icon-info", content);

        Assert.DoesNotContain("🔥 471 kcal", content);
        Assert.DoesNotContain("💪 18.8g P", content);
        Assert.DoesNotContain("🌾 67g C", content);
        Assert.DoesNotContain("🥑 15g F", content);
        Assert.DoesNotContain("🥗 8.2g Fib", content);
        Assert.DoesNotContain("🍬 4.1g Sug", content);
        Assert.DoesNotContain("ℹ️", content);
    }

    [Fact]
    public void CompanionCardHtml_UsesSvgSpriteSymbols()
    {
        var companionPath = Path.Combine(_wwwroot, "partials", "companion-card.html");
        Assert.True(File.Exists(companionPath), $"companion-card.html must exist at {companionPath}");
        var content = File.ReadAllText(companionPath);

        Assert.Contains("assets/icons/sprite.svg#icon-stethoscope", content);
        Assert.Contains("assets/icons/sprite.svg#icon-fire", content);

        Assert.DoesNotContain("👨‍⚕️", content);
        Assert.DoesNotContain("🔥 7-Day", content);
    }

    [Fact]
    public void TransparencyModalHtml_UsesSvgSpriteSymbols()
    {
        var transparencyPath = Path.Combine(_wwwroot, "partials", "transparency-modal.html");
        Assert.True(File.Exists(transparencyPath), $"transparency-modal.html must exist at {transparencyPath}");
        var content = File.ReadAllText(transparencyPath);

        Assert.Contains("assets/icons/sprite.svg#icon-printer", content);
        Assert.Contains("assets/icons/sprite.svg#icon-check", content);

        Assert.DoesNotContain("🖨️ Print", content);
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
        Assert.Contains("icon-fire", content);
        Assert.Contains("icon-muscle", content);
        Assert.Contains("icon-grain", content);
        Assert.Contains("icon-lipid", content);
        Assert.Contains("icon-leaf", content);
        Assert.Contains("icon-cube", content);
        Assert.Contains("icon-shaker", content);

        Assert.DoesNotContain("✏️ Edit", content);
        Assert.DoesNotContain("🗑️ Delete", content);
        Assert.DoesNotContain("🔥 ${cals}", content);
        Assert.DoesNotContain("💪 ${protein}", content);
        Assert.DoesNotContain("🌾 ${carbs}", content);
        Assert.DoesNotContain("🥑 ${fat}", content);
        Assert.DoesNotContain("🥗 ${fiber}", content);
        Assert.DoesNotContain("🍬 ${sugar}", content);
        Assert.DoesNotContain("🧂 ${sodium}", content);
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
        Assert.Contains(".icon-svg", content);
        Assert.Contains(".icon-tab", content);
        Assert.Contains(".icon-btn", content);
        Assert.Contains(".icon-heading", content);
        Assert.Contains(".icon-status-online", content);
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

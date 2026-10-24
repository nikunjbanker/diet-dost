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

public class ModalStackingAndLifecycleContractTests
{
    private readonly string _wwwroot;

    public ModalStackingAndLifecycleContractTests()
    {
        _wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/wwwroot"));
    }

    [Fact]
    public void StylesCss_EliminatesLowZIndexOverlays_ThatBreakModalStacking()
    {
        var stylesPath = Path.Combine(_wwwroot, "styles.css");
        Assert.True(File.Exists(stylesPath), $"styles.css must exist at {stylesPath}");
        var stylesContent = File.ReadAllText(stylesPath);

        // Assert delete-modal-overlay does not have z-index: 120 (must be >= 100010)
        Assert.DoesNotMatch(@"\.delete-modal-overlay\s*\{[^}]*z-index:\s*120;", stylesContent);

        // Assert review-lightbox does not have z-index: 1200 (must be >= 100010)
        Assert.DoesNotMatch(@"\.review-lightbox-overlay\s*\{[^}]*z-index:\s*1200;", stylesContent);
    }

    [Fact]
    public void AdminModalHtml_EliminatesHardcodedInlineZIndex()
    {
        var adminModalPath = Path.Combine(_wwwroot, "partials", "admin-modal.html");
        Assert.True(File.Exists(adminModalPath), $"admin-modal.html must exist at {adminModalPath}");
        var content = File.ReadAllText(adminModalPath);

        // Assert admin edit user modal does not hardcode z-index: 10005 (which renders behind parent)
        Assert.DoesNotContain("z-index: 10005", content);
    }

    [Fact]
    public void ProgressModalJs_ClosesModalOnSaveAndCancel()
    {
        var progressJsPath = Path.Combine(_wwwroot, "js", "ui", "progress-modal.js");
        Assert.True(File.Exists(progressJsPath), $"progress-modal.js must exist at {progressJsPath}");
        var content = File.ReadAllText(progressJsPath);

        // Assert handleSubmit invokes this.close() instead of only switching tab
        Assert.DoesNotContain("this.switchTab('pane-timeline-gallery');", content);
        Assert.Contains("this.close();", content);

        // Assert cancel button closes modal instead of switching tab
        Assert.DoesNotContain("this.switchTab('pane-face-progress')", content);
    }

    [Fact]
    public void ProgressModalHtml_RemovesDuplicateInlineOnClickHandlers()
    {
        var progressHtmlPath = Path.Combine(_wwwroot, "partials", "progress-modal.html");
        Assert.True(File.Exists(progressHtmlPath), $"progress-modal.html must exist at {progressHtmlPath}");
        var content = File.ReadAllText(progressHtmlPath);

        // Assert tabs do not have redundant inline onclick that re-triggers openProgressModal
        Assert.DoesNotContain("onclick=\"openProgressModal('pane-face-progress')\"", content);
        Assert.DoesNotContain("onclick=\"openProgressModal('pane-body-progress')\"", content);
        Assert.DoesNotContain("onclick=\"openProgressModal('pane-timeline-gallery')\"", content);
    }

    [Fact]
    public void ModalStackManager_ScriptIsCreatedAndReferencedInIndexHtml()
    {
        var stackManagerPath = Path.Combine(_wwwroot, "js", "ui", "modal-stack-manager.js");
        Assert.True(File.Exists(stackManagerPath), $"modal-stack-manager.js must exist at {stackManagerPath}");

        var indexHtmlPath = Path.Combine(_wwwroot, "index.html");
        var indexContent = File.ReadAllText(indexHtmlPath);
        Assert.Contains("modal-stack-manager.js", indexContent);
    }

    [Fact]
    public void ReviewModalHtml_HasAccessibleHeaderCloseButton()
    {
        var reviewModalPath = Path.Combine(_wwwroot, "partials", "review-modal.html");
        Assert.True(File.Exists(reviewModalPath), $"review-modal.html must exist at {reviewModalPath}");
        var content = File.ReadAllText(reviewModalPath);

        // Assert review modal has an accessible close button in header
        Assert.Contains("id=\"btn-review-header-close\"", content);
    }

    [Fact]
    public void ProgressAndSelfPhotos_StylesAndAspectRatios_ShowFullUncutPhotosWithoutCropping()
    {
        var stylesPath = Path.Combine(_wwwroot, "styles.css");
        Assert.True(File.Exists(stylesPath), $"styles.css must exist at {stylesPath}");
        var stylesContent = File.ReadAllText(stylesPath);

        // Assert .progress-img uses object-fit: contain (and not cover)
        var progressImgMatch = Regex.Match(stylesContent, @"\.progress-img\s*\{([^}]+)\}");
        Assert.True(progressImgMatch.Success, ".progress-img must be defined in styles.css");
        Assert.Contains("object-fit: contain", progressImgMatch.Groups[1].Value);
        Assert.DoesNotContain("object-fit: cover", progressImgMatch.Groups[1].Value);

        // Assert .detail-img uses object-fit: contain (and not cover)
        var detailImgMatch = Regex.Match(stylesContent, @"\.detail-img\s*\{([^}]+)\}");
        Assert.True(detailImgMatch.Success, ".detail-img must be defined in styles.css");
        Assert.Contains("object-fit: contain", detailImgMatch.Groups[1].Value);
        Assert.DoesNotContain("object-fit: cover", detailImgMatch.Groups[1].Value);

        // Assert .timeline-thumb uses object-fit: contain (and not cover)
        var timelineThumbMatch = Regex.Match(stylesContent, @"\.timeline-thumb\s*\{([^}]+)\}");
        Assert.True(timelineThumbMatch.Success, ".timeline-thumb must be defined in styles.css");
        Assert.Contains("object-fit: contain", timelineThumbMatch.Groups[1].Value);
        Assert.DoesNotContain("object-fit: cover", timelineThumbMatch.Groups[1].Value);

        // Assert progress-img-preview in progress-modal.html has object-fit: contain
        var progressModalHtmlPath = Path.Combine(_wwwroot, "partials", "progress-modal.html");
        Assert.True(File.Exists(progressModalHtmlPath), $"progress-modal.html must exist at {progressModalHtmlPath}");
        var progressModalHtml = File.ReadAllText(progressModalHtmlPath);
        Assert.Contains("progress-img-preview", progressModalHtml);
        Assert.Matches(@"<img[^>]*id=""progress-img-preview""[^>]*object-fit:\s*contain", progressModalHtml);
    }
}


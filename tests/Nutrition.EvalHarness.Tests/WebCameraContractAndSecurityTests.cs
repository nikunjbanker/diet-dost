/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Http;
using Nutrition.WebGateway.Middleware;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class WebCameraContractAndSecurityTests
{
    private readonly string _wwwroot;

    public WebCameraContractAndSecurityTests()
    {
        _wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/wwwroot"));
    }

    [Fact]
    public async Task PermissionsPolicy_MustAllowCameraAndGeolocationForSelf_AndDisallowMicrophone()
    {
        var middleware = new SecurityHeadersMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        var policy = context.Response.Headers["Permissions-Policy"].ToString();

        // Must permit camera and geolocation strictly for first-party self origin
        Assert.Contains("camera=(self)", policy);
        Assert.Contains("geolocation=(self)", policy);

        // Must strictly disallow microphone
        Assert.Contains("microphone=()", policy);

        // Must not disallow camera or geolocation
        Assert.DoesNotContain("camera=()", policy);
        Assert.DoesNotContain("geolocation=()", policy);
    }

    [Fact]
    public void CameraModalPartial_MustExistAndContainViewfinderZoomAndCropContracts()
    {
        var partialPath = Path.Combine(_wwwroot, "partials", "camera-modal.html");
        Assert.True(File.Exists(partialPath), $"camera-modal.html must exist at {partialPath}");

        var content = File.ReadAllText(partialPath);

        // Viewfinder video contracts
        Assert.Contains("id=\"camera-video-stream\"", content);
        Assert.Contains("playsinline", content);
        Assert.Contains("autoplay", content);
        Assert.Contains("muted", content);

        // Zoom UI contracts
        Assert.Contains("btn-zoom-1x", content);
        Assert.Contains("btn-zoom-2x", content);
        Assert.Contains("btn-zoom-3x", content);

        // Camera control contracts
        Assert.Contains("id=\"btn-shutter-snap\"", content);
        Assert.Contains("id=\"btn-camera-flip\"", content);
        Assert.Contains("id=\"btn-camera-torch\"", content);
        Assert.Contains("id=\"btn-camera-gallery-fallback\"", content);
        Assert.Contains("id=\"btn-close-camera\"", content);

        // Post-capture crop & region selection contracts
        Assert.Contains("id=\"camera-crop-container\"", content);
        Assert.Contains("id=\"btn-crop-preset-square\"", content);
        Assert.Contains("id=\"btn-crop-preset-standard\"", content);
        Assert.Contains("id=\"btn-crop-preset-full\"", content);
        Assert.Contains("id=\"btn-confirm-use-photo\"", content);
        Assert.Contains("id=\"btn-retake-photo\"", content);
    }

    [Fact]
    public void MealLoggerPartial_MustContainTakePhotoAndGalleryActions()
    {
        var partialPath = Path.Combine(_wwwroot, "partials", "meal-logger.html");
        Assert.True(File.Exists(partialPath), $"meal-logger.html must exist at {partialPath}");

        var content = File.ReadAllText(partialPath);

        // Must have dedicated Take Photo action
        Assert.Contains("id=\"btn-take-photo\"", content);

        // Must have dedicated Choose from Gallery action
        Assert.Contains("id=\"btn-choose-gallery\"", content);

        // Must have non-WebRTC fallback input
        Assert.Contains("id=\"camera-file-fallback\"", content);
        Assert.Contains("capture=\"environment\"", content);
    }

    [Fact]
    public void ProgressModalPartial_MustContainCameraCheckInAction()
    {
        var partialPath = Path.Combine(_wwwroot, "partials", "progress-modal.html");
        Assert.True(File.Exists(partialPath), $"progress-modal.html must exist at {partialPath}");

        var content = File.ReadAllText(partialPath);

        // Must provide Take Photo check-in action
        Assert.Contains("id=\"btn-progress-take-photo\"", content);
        Assert.Contains("id=\"btn-progress-choose-gallery\"", content);
    }

    [Fact]
    public void IndexHtml_MustIncludeCameraModalPartialAndScripts()
    {
        var indexPath = Path.Combine(_wwwroot, "index.html");
        Assert.True(File.Exists(indexPath), $"index.html must exist at {indexPath}");

        var content = File.ReadAllText(indexPath);

        // Must declare camera-modal.html include
        Assert.Contains("data-include=\"partials/camera-modal.html\"", content);
    }

    [Fact]
    public void CameraModalAndCallers_MustSupportGalleryCroppingContract()
    {
        var cameraModalJs = Path.Combine(_wwwroot, "js", "ui", "camera-modal.js");
        Assert.True(File.Exists(cameraModalJs));
        var cameraJsContent = File.ReadAllText(cameraModalJs);
        Assert.Contains("openCrop(", cameraJsContent);

        var mealLoggerJs = Path.Combine(_wwwroot, "js", "ui", "meal-logger.js");
        Assert.True(File.Exists(mealLoggerJs));
        var mealLoggerContent = File.ReadAllText(mealLoggerJs);
        Assert.Contains("openCrop(", mealLoggerContent);

        var progressModalJs = Path.Combine(_wwwroot, "js", "ui", "progress-modal.js");
        Assert.True(File.Exists(progressModalJs));
        var progressContent = File.ReadAllText(progressModalJs);
        Assert.Contains("openCrop(", progressContent);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Options;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public sealed class PhotoStorageAndStaticFileServingTests : IDisposable
{
    private readonly string _tempRoot;

    public PhotoStorageAndStaticFileServingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "diet_dost_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
            // Ignore cleanup failure in temp directory
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveMealPhotoAsync_StoresInConfiguredExternalWebRootPath_AndReturnsUploadsPath()
    {
        // Simulates Azure File Share SMB mount at /app/data/wwwroot
        var customWebRoot = Path.Combine(_tempRoot, "data", "wwwroot");
        var options = Options.Create(new StorageOptions { WebRootPath = customWebRoot });
        var service = new LocalPhotoStorageService(options, NullLogger<LocalPhotoStorageService>.Instance);

        var sampleBytes = Encoding.UTF8.GetBytes("fake_jpeg_image_data_payload");
        using var stream = new MemoryStream(sampleBytes);

        var resultUrl = await service.SaveMealPhotoAsync(stream, "lunch_thali.jpg", "image/jpeg");

        Assert.NotNull(resultUrl);
        Assert.StartsWith("/uploads/meals/", resultUrl);

        var relativePath = resultUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var expectedPhysicalPath = Path.Combine(customWebRoot, relativePath);

        Assert.True(File.Exists(expectedPhysicalPath), $"File should exist at {expectedPhysicalPath}");
        var savedContent = await File.ReadAllBytesAsync(expectedPhysicalPath);
        Assert.Equal(sampleBytes, savedContent);
    }

    [Fact]
    public async Task SaveMealPhotoAsync_WithUserId_StoresInUserScopedSubfolder()
    {
        var customWebRoot = Path.Combine(_tempRoot, "data", "wwwroot");
        var options = Options.Create(new StorageOptions { WebRootPath = customWebRoot });
        var service = new LocalPhotoStorageService(options, NullLogger<LocalPhotoStorageService>.Instance);

        var sampleBytes = Encoding.UTF8.GetBytes("fake_user_meal_photo_bytes");
        using var stream = new MemoryStream(sampleBytes);

        var resultUrl = await service.SaveMealPhotoAsync("user-demo-42", stream, "paneer_tikka.jpg", "image/jpeg");

        Assert.NotNull(resultUrl);
        Assert.StartsWith("/uploads/users/user-demo-42/meals/", resultUrl);

        var relativePath = resultUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var expectedPhysicalPath = Path.Combine(customWebRoot, relativePath);

        Assert.True(File.Exists(expectedPhysicalPath), $"Physical file must exist at {expectedPhysicalPath}");
        var savedContent = await File.ReadAllBytesAsync(expectedPhysicalPath);
        Assert.Equal(sampleBytes, savedContent);
    }

    [Fact]
    public async Task SaveProgressPhotoAsync_WithUserId_StoresInUserScopedProgressSubfolder()
    {
        var customWebRoot = Path.Combine(_tempRoot, "data", "wwwroot");
        var options = Options.Create(new StorageOptions { WebRootPath = customWebRoot });
        var service = new LocalPhotoStorageService(options, NullLogger<LocalPhotoStorageService>.Instance);

        var sampleBytes = Encoding.UTF8.GetBytes("fake_user_progress_photo_bytes");
        using var stream = new MemoryStream(sampleBytes);

        var resultUrl = await service.SaveProgressPhotoAsync("user-demo-42", stream, "face_checkin.jpg", "image/jpeg");

        Assert.NotNull(resultUrl);
        Assert.StartsWith("/uploads/users/user-demo-42/progress/", resultUrl);

        var relativePath = resultUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var expectedPhysicalPath = Path.Combine(customWebRoot, relativePath);

        Assert.True(File.Exists(expectedPhysicalPath), $"Physical file must exist at {expectedPhysicalPath}");
        var savedContent = await File.ReadAllBytesAsync(expectedPhysicalPath);
        Assert.Equal(sampleBytes, savedContent);
    }

    [Fact]
    public async Task PhysicalFileProvider_TargetingUploadsDirectory_LocatesUploadedPhotos()
    {
        // Simulates ASP.NET Core PhysicalFileProvider pointing to /app/data/wwwroot/uploads
        var customWebRoot = Path.Combine(_tempRoot, "data", "wwwroot");
        var uploadsDir = Path.Combine(customWebRoot, "uploads");
        var mealsDir = Path.Combine(uploadsDir, "meals");
        Directory.CreateDirectory(mealsDir);

        var testFileName = "progress_face_" + Guid.NewGuid().ToString("N") + ".jpg";
        var physicalFilePath = Path.Combine(mealsDir, testFileName);
        await File.WriteAllTextAsync(physicalFilePath, "sample-face-progress-content");

        // Provider registered with root at uploadsDir
        var provider = new PhysicalFileProvider(uploadsDir);
        var fileInfo = provider.GetFileInfo($"meals/{testFileName}");

        Assert.True(fileInfo.Exists, "PhysicalFileProvider must locate files inside the mounted uploads path");
        Assert.Equal(testFileName, fileInfo.Name);
    }

    [Fact]
    public void StaticFileRegistration_UploadsMappingConfigured_WhenExternalWebRootPathExists()
    {
        // Verify that WebApplicationExtensions.cs has the secondary StaticFile middleware registration
        // mapped to the /uploads request path using PhysicalFileProvider.
        var webAppExtPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/Extensions/WebApplicationExtensions.cs"));
        Assert.True(File.Exists(webAppExtPath), $"WebApplicationExtensions.cs must exist at {webAppExtPath}");

        var content = File.ReadAllText(webAppExtPath);

        // This assertion verifies the presence of PhysicalFileProvider registration for /uploads
        Assert.Contains("UseStaticFiles", content);
        Assert.Contains("PhysicalFileProvider", content);
        Assert.Contains("/uploads", content);
    }

    [Fact]
    public void DatabaseInitialization_SeedsCorrectMealPhotoFilename()
    {
        // Verify that DatabaseInitializationExtensions uses sample_lunch_thali.jpg instead of missing sample_thali.jpg
        var dbInitPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs"));
        Assert.True(File.Exists(dbInitPath), $"DatabaseInitializationExtensions.cs must exist at {dbInitPath}");

        var content = File.ReadAllText(dbInitPath);
        Assert.DoesNotContain("sample_thali.jpg", content);
        Assert.Contains("sample_lunch_thali.jpg", content);
    }
}

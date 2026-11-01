/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Features.ProgressPhotos.Commands.UploadProgressPhoto;
using Nutrition.Application.Features.ProgressPhotos.DTOs;
using Nutrition.Application.Features.ProgressPhotos.Queries.CompareProgressPhotos;
using Nutrition.Application.Features.ProgressPhotos.Queries.GetProgressPhotos;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Progress;
using Nutrition.WebGateway.Controllers;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public sealed class MediaStreamingSecurityTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryPhotoRepo _photoRepo;
    private readonly TestWebHostEnvironment _env;
    private readonly IOptions<StorageOptions> _storageOptions;

    public MediaStreamingSecurityTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "diet_dost_media_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _photoRepo = new InMemoryPhotoRepo();
        _env = new TestWebHostEnvironment
        {
            ContentRootPath = _tempRoot,
            WebRootPath = Path.Combine(_tempRoot, "wwwroot")
        };

        _storageOptions = Options.Create(new StorageOptions
        {
            WebRootPath = Path.Combine(_tempRoot, "data", "wwwroot")
        });
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
            // Ignore temp cleanup errors
        }
    }

    private MediaController CreateController(string userId, string role = "User")
    {
        var controller = new MediaController(_photoRepo, _storageOptions, _env);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Role, role),
            new("tier", UserTier.Premium.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetProgressPhoto_DifferentUser_ReturnsForbidResult_PreventingIdor()
    {
        // Vector 1: Insecure Direct Object Reference (IDOR)
        var photoId = "photo-123";
        var victimUserId = "usr_victim_99";
        var attackerUserId = "usr_attacker_42";

        var photo = new ProgressPhoto
        {
            Id = photoId,
            UserId = victimUserId,
            PhotoUri = "/uploads/users/usr_victim_99/progress/face_secret.jpg",
            PhotoType = ProgressPhotoType.Face
        };

        await _photoRepo.AddAsync(photo);

        var controller = CreateController(attackerUserId, "User");

        var result = await controller.GetProgressPhoto(photoId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetProgressPhoto_Owner_StreamsPhysicalFileWithSecurityHeaders()
    {
        // Vector 3: User streaming their own photo from storage/file share
        var photoId = "photo-owner-1";
        var ownerUserId = "usr_owner_10";
        var fileName = "20261101_face_checkin.jpg";
        var relativeStoragePath = $"/uploads/users/{ownerUserId}/progress/{fileName}";

        var photo = new ProgressPhoto
        {
            Id = photoId,
            UserId = ownerUserId,
            PhotoUri = relativeStoragePath,
            PhotoType = ProgressPhotoType.Face
        };

        await _photoRepo.AddAsync(photo);

        // Create the actual physical file in the configured StorageOptions file share path
        var smbWebRoot = _storageOptions.Value.WebRootPath;
        var destinationDir = Path.Combine(smbWebRoot, "uploads", "users", ownerUserId, "progress");
        Directory.CreateDirectory(destinationDir);
        var filePath = Path.Combine(destinationDir, fileName);
        await File.WriteAllTextAsync(filePath, "test-image-data-payload");

        var controller = CreateController(ownerUserId, "User");

        var result = await controller.GetProgressPhoto(photoId, CancellationToken.None);

        var physicalFile = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal(filePath, physicalFile.FileName);
        Assert.Equal("image/jpeg", physicalFile.ContentType);
        Assert.True(physicalFile.EnableRangeProcessing);

        // Security headers assert
        var responseHeaders = controller.Response.Headers;
        Assert.Equal("nosniff", responseHeaders["X-Content-Type-Options"].ToString());
        Assert.Contains("default-src 'none'", responseHeaders["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task GetProgressPhoto_SvgFile_ReturnsSvgXmlMimeType()
    {
        // Vector 6: SVG Content-Type verification
        var photoId = "seed-face-svg";
        var userId = "usr_demo_1";
        var relativeStoragePath = "/uploads/progress/face_baseline.svg";

        var photo = new ProgressPhoto
        {
            Id = photoId,
            UserId = userId,
            PhotoUri = relativeStoragePath,
            PhotoType = ProgressPhotoType.Face
        };

        await _photoRepo.AddAsync(photo);

        // Put SVG file in wwwroot fallback location
        var wwwrootDir = Path.Combine(_tempRoot, "wwwroot", "uploads", "progress");
        Directory.CreateDirectory(wwwrootDir);
        var filePath = Path.Combine(wwwrootDir, "face_baseline.svg");
        await File.WriteAllTextAsync(filePath, "<svg><circle r='10'/></svg>");

        var controller = CreateController(userId, "User");

        var result = await controller.GetProgressPhoto(photoId, CancellationToken.None);

        var physicalFile = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("image/svg+xml", physicalFile.ContentType);
    }

    [Fact]
    public async Task GetProgressPhoto_SuperAdmin_CanAccessSoftDeletedPhoto()
    {
        // Vector 7: SuperAdmin compliance audit of soft-deleted records
        var photoId = "photo-deleted-1";
        var ownerUserId = "usr_owner_10";
        var fileName = "deleted_face.jpg";
        var relativeStoragePath = $"/uploads/users/{ownerUserId}/progress/{fileName}";

        var photo = new ProgressPhoto
        {
            Id = photoId,
            UserId = ownerUserId,
            PhotoUri = relativeStoragePath,
            PhotoType = ProgressPhotoType.Face,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        await _photoRepo.AddAsync(photo);

        var smbWebRoot = _storageOptions.Value.WebRootPath;
        var destinationDir = Path.Combine(smbWebRoot, "uploads", "users", ownerUserId, "progress");
        Directory.CreateDirectory(destinationDir);
        var filePath = Path.Combine(destinationDir, fileName);
        await File.WriteAllTextAsync(filePath, "deleted-image-bytes");

        // 1. Regular user gets NotFound
        var userController = CreateController(ownerUserId, "User");
        var userResult = await userController.GetProgressPhoto(photoId, CancellationToken.None);
        Assert.IsType<NotFoundResult>(userResult);

        // 2. SuperAdmin gets 200 PhysicalFileResult for audit
        var adminController = CreateController("superadmin-root", "SuperAdmin");
        var adminResult = await adminController.GetProgressPhoto(photoId, CancellationToken.None);
        var adminPhysicalFile = Assert.IsType<PhysicalFileResult>(adminResult);
        Assert.Equal(filePath, adminPhysicalFile.FileName);
    }

    [Fact]
    public async Task CQRS_GetProgressPhotosQuery_ProjectsPhotoUriToMediaStreamingEndpoint()
    {
        // Vector 10: Clean Architecture handler URL projection
        var userId = "usr_test_query";
        var repo = new InMemoryPhotoRepo();
        await repo.AddAsync(new ProgressPhoto
        {
            Id = "photo-q-1",
            UserId = userId,
            PhotoUri = $"/uploads/users/{userId}/progress/img1.jpg",
            PhotoType = ProgressPhotoType.Face,
            CapturedAtUtc = DateTime.UtcNow
        });

        var tierConfig = new FakeTierConfigService();
        var handler = new GetProgressPhotosQueryHandler(repo, tierConfig);
        var query = new GetProgressPhotosQuery(userId, userId, UserTier.Premium, false, null);

        var result = await handler.HandleAsync(query);

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("/api/media/progress/photo-q-1", result.Data![0].PhotoUri);
    }

    [Fact]
    public async Task CQRS_CompareProgressPhotosQuery_ProjectsPhotoUriToMediaStreamingEndpoint()
    {
        var userId = "usr_test_compare";
        var repo = new InMemoryPhotoRepo();
        await repo.AddAsync(new ProgressPhoto
        {
            Id = "face-base-1",
            UserId = userId,
            PhotoUri = "/uploads/progress/face_baseline.svg",
            PhotoType = ProgressPhotoType.Face,
            IsBaseline = true,
            CapturedAtUtc = DateTime.UtcNow.AddDays(-30)
        });
        await repo.AddAsync(new ProgressPhoto
        {
            Id = "face-curr-1",
            UserId = userId,
            PhotoUri = $"/uploads/users/{userId}/progress/face_checkin.jpg",
            PhotoType = ProgressPhotoType.Face,
            IsBaseline = false,
            CapturedAtUtc = DateTime.UtcNow
        });

        var tierConfig = new FakeTierConfigService();
        var handler = new CompareProgressPhotosQueryHandler(repo, tierConfig);
        var query = new CompareProgressPhotosQuery(userId, userId, UserTier.Premium, false);

        var result = await handler.HandleAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data!.BaselineFace);
        Assert.Equal("/api/media/progress/face-base-1", result.Data!.BaselineFace.PhotoUri);
        Assert.NotNull(result.Data!.CurrentFace);
        Assert.Equal("/api/media/progress/face-curr-1", result.Data!.CurrentFace.PhotoUri);
        Assert.Equal("/api/media/progress/face-base-1", result.Data!.AllPhotos[0].PhotoUri);
        Assert.Equal("/api/media/progress/face-curr-1", result.Data!.AllPhotos[1].PhotoUri);
    }

    [Fact]
    public async Task CQRS_UploadProgressPhotoCommand_ProjectsPhotoUriInResultDto()
    {
        var userId = "usr_upload_test";
        var repo = new InMemoryPhotoRepo();
        var storage = new FakePhotoStorageService();
        var uow = new FakeUnitOfWork();

        var handler = new UploadProgressPhotoCommandHandler(
            repo,
            storage,
            uow,
            NullLogger<UploadProgressPhotoCommandHandler>.Instance);

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("fake_photo_bytes"));
        var command = new UploadProgressPhotoCommand(
            userId,
            memoryStream,
            "my_face.jpg",
            "image/jpeg",
            82.5,
            ProgressPhotoType.Face,
            false,
            "Checkin notes",
            null);

        var result = await handler.HandleAsync(command);

        Assert.True(result.Succeeded);
        Assert.StartsWith("/api/media/progress/", result.Data!.Photo.PhotoUri);
    }

    // --- In-Memory Test Fakes ---

    private class InMemoryPhotoRepo : IRepository<ProgressPhoto>
    {
        private readonly List<ProgressPhoto> _photos = new();

        public IQueryable<ProgressPhoto> Query() => _photos.AsQueryable();

        public Task<ProgressPhoto?> GetByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));

        public Task<List<ProgressPhoto>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(_photos.ToList());

        public Task<List<ProgressPhoto>> FindAsync(Expression<Func<ProgressPhoto, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_photos.AsQueryable().Where(predicate).ToList());

        public Task<ProgressPhoto?> FirstOrDefaultAsync(Expression<Func<ProgressPhoto, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_photos.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> AnyAsync(Expression<Func<ProgressPhoto, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_photos.AsQueryable().Any(predicate));

        public Task<int> CountAsync(Expression<Func<ProgressPhoto, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_photos.AsQueryable().Count(predicate));

        public Task<List<ProgressPhoto>> FindDeletedAsync(Expression<Func<ProgressPhoto, bool>>? predicate = null, CancellationToken ct = default) =>
            Task.FromResult(_photos.Where(p => p.IsDeleted).ToList());

        public Task AddAsync(ProgressPhoto entity, CancellationToken ct = default)
        {
            _photos.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProgressPhoto entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(ProgressPhoto entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteRangeAsync(IEnumerable<ProgressPhoto> entities, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeTierConfigService : ITierConfigurationService
    {
        public Task<TierFeatureConfiguration> GetConfigurationAsync(UserTier tier, CancellationToken ct = default) =>
            Task.FromResult(new TierFeatureConfiguration { Tier = tier, AllowPhotoCompare = true });

        public Task<List<TierFeatureConfiguration>> GetAllConfigurationsAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<TierFeatureConfiguration>());

        public Task<TierFeatureConfiguration> UpdateConfigurationAsync(TierFeatureConfiguration configuration, CancellationToken ct = default) =>
            Task.FromResult(configuration);
    }

    private class FakePhotoStorageService : IPhotoStorageService
    {
        public Task<string> SaveMealPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult($"/uploads/users/{userId}/meals/meal.jpg");

        public Task<string> SaveMealPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult("/uploads/meals/meal.jpg");

        public Task<string> SaveProgressPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult($"/uploads/users/{userId}/progress/prog_{Guid.NewGuid():N}.jpg");

        public Task<string> SaveProgressPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) =>
            Task.FromResult("/uploads/progress/prog.jpg");

        public Task<bool> DeletePhotoAsync(string relativePathOrUrl, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Nutrition.WebGateway";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

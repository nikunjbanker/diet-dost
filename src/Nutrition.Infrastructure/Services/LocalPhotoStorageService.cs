/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Common.Security;
using Nutrition.Infrastructure.Security;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter implementing IPhotoStorageService for local disk / wwwroot persistence.
/// Pure BCL implementation decoupled from ASP.NET Core presentation contracts.
/// Persists user uploads in user-scoped subdirectories (uploads/users/{userId}/meals and uploads/users/{userId}/progress)
/// and enforces zero physical deletion policy for operational auditability and troubleshooting.
/// </summary>
public class LocalPhotoStorageService : IPhotoStorageService
{
    private readonly string _webRootPath;
    private readonly ILogger<LocalPhotoStorageService> _logger;

    public LocalPhotoStorageService(
        IOptions<StorageOptions> storageOptions,
        ILogger<LocalPhotoStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var configuredPath = storageOptions?.Value?.WebRootPath;
        _webRootPath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> SaveMealPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SavePhotoInternalAsync(userId, "meals", fileStream, originalFileName, contentType, ct);
    }

    public async Task<string> SaveMealPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SaveMealPhotoAsync(null, fileStream, originalFileName, contentType, ct);
    }

    public async Task<string> SaveProgressPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SavePhotoInternalAsync(userId, "progress", fileStream, originalFileName, contentType, ct);
    }

    public async Task<string> SaveProgressPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SaveProgressPhotoAsync(null, fileStream, originalFileName, contentType, ct);
    }

    public Task<bool> DeletePhotoAsync(string relativePathOrUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePathOrUrl))
            return Task.FromResult(false);

        // Zero Physical Deletion Policy: Physical assets are strictly preserved for troubleshooting & forensic audit
        _logger.LogInformation("[STORAGE] Bypassing physical deletion per zero-physical-delete retention policy: {Path}", relativePathOrUrl);
        return Task.FromResult(true);
    }

    private async Task<string> SavePhotoInternalAsync(
        string? userId,
        string subFolder,
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        string destinationFolder;
        string returnBaseUrl;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var sanitizedUser = PhotoPathSanitizer.SanitizeUserId(userId);
            destinationFolder = Path.Combine(_webRootPath, "uploads", "users", sanitizedUser, subFolder);
            returnBaseUrl = $"/uploads/users/{sanitizedUser}/{subFolder}/";
        }
        else
        {
            destinationFolder = Path.Combine(_webRootPath, "uploads", subFolder);
            returnBaseUrl = $"/uploads/{subFolder}/";
        }

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var uniqueFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(destinationFolder, uniqueFileName);

        using var sanitizedStream = await ImageSanitizer.SanitizeAndStripExifAsync(fileStream, contentType, ct);
        using var fileOutput = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await sanitizedStream.CopyToAsync(fileOutput, ct);

        return PhotoPathSanitizer.NormalizeToRelativePath(returnBaseUrl + uniqueFileName);
    }
}

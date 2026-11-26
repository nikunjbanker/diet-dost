/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Common.Security;
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.Queries.GetProgressPhotoStream;

public record GetProgressPhotoStreamQuery(
    string PhotoId,
    string CurrentUserId,
    bool IsAdminOrSuper,
    string ContentRootPath
) : IQuery<Result<ProgressPhotoStreamDto>>;

public sealed record ProgressPhotoStreamDto(string FullPath, string ContentType);

public sealed class GetProgressPhotoStreamQueryHandler : IQueryHandler<GetProgressPhotoStreamQuery, Result<ProgressPhotoStreamDto>>
{
    private readonly IReadRepository<ProgressPhoto> _photoRepo;
    private readonly IOptions<StorageOptions> _storageOptions;

    public GetProgressPhotoStreamQueryHandler(
        IReadRepository<ProgressPhoto> photoRepo,
        IOptions<StorageOptions> storageOptions)
    {
        _photoRepo = photoRepo ?? throw new ArgumentNullException(nameof(photoRepo));
        _storageOptions = storageOptions ?? throw new ArgumentNullException(nameof(storageOptions));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3003:Review code for file path injection vulnerabilities",
        Justification = "Photo URI is retrieved from a verified database entity, sanitized against path traversal, and verified to remain strictly within the web root.")]
    public async Task<Result<ProgressPhotoStreamDto>> HandleAsync(GetProgressPhotoStreamQuery request, CancellationToken ct = default)
    {
        var photo = await _photoRepo.GetByIdAsync(request.PhotoId, ct);
        if (photo == null || (photo.IsDeleted && !request.IsAdminOrSuper))
        {
            return Result<ProgressPhotoStreamDto>.NotFound("Progress photo not found.");
        }

        if (photo.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<ProgressPhotoStreamDto>.Forbidden("Access denied.");
        }

        var primaryWebRoot = !string.IsNullOrWhiteSpace(_storageOptions.Value?.WebRootPath)
            ? _storageOptions.Value.WebRootPath
            : Path.Combine(request.ContentRootPath, "wwwroot");

        var canonicalRoot = Path.GetFullPath(primaryWebRoot);
        var sanitizedRel = PhotoPathSanitizer.NormalizeToRelativePath(photo.PhotoUri).TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, sanitizedRel));

        if (!fullPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ProgressPhotoStreamDto>.Forbidden("Invalid photo path.");
        }

        if (!File.Exists(fullPath))
        {
            var fallbackWebRoot = Path.GetFullPath(Path.Combine(request.ContentRootPath, "wwwroot"));
            var fallbackFullPath = Path.GetFullPath(Path.Combine(fallbackWebRoot, sanitizedRel));
            if (fallbackFullPath.StartsWith(fallbackWebRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fallbackFullPath))
            {
                fullPath = fallbackFullPath;
            }
            else
            {
                return Result<ProgressPhotoStreamDto>.NotFound("Photo asset not found.");
            }
        }

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };

        return Result<ProgressPhotoStreamDto>.Success(new ProgressPhotoStreamDto(fullPath, contentType));
    }
}

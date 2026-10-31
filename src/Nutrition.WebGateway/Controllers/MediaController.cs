/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Common.Security;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Progress;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Authenticated Media Controller for sensitive user progress photos,
/// enforcing role-based and user-scoped authorization per GAP-05 and DPDPA 2023.
/// </summary>
[Authorize]
[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IWebHostEnvironment _env;

    public MediaController(
        IRepository<ProgressPhoto> photoRepo,
        IOptions<StorageOptions> storageOptions,
        IWebHostEnvironment env)
    {
        _photoRepo = photoRepo;
        _storageOptions = storageOptions;
        _env = env;
    }

    [HttpGet("progress/{id}")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3003:Review code for file path injection vulnerabilities",
        Justification = "Photo URI is retrieved from a verified database entity, sanitized against path traversal, and verified to remain strictly within the web root.")]
    public async Task<IActionResult> GetProgressPhoto(string id, CancellationToken ct)
    {
        var photo = await _photoRepo.GetByIdAsync(id, ct);
        if (photo == null || photo.IsDeleted)
        {
            return NotFound();
        }

        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.SuperAdmin.ToString());

        if (photo.UserId != currentUserId && !isAdmin)
        {
            return Forbid();
        }

        var webRoot = !string.IsNullOrWhiteSpace(_storageOptions.Value?.WebRootPath)
            ? _storageOptions.Value.WebRootPath
            : Path.Combine(_env.ContentRootPath, "wwwroot");

        var canonicalRoot = Path.GetFullPath(webRoot);
        var sanitizedRel = PhotoPathSanitizer.NormalizeToRelativePath(photo.PhotoUri).TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, sanitizedRel));

        if (!fullPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }
}

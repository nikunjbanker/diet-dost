/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.SuperAdmin.ToString());

        if (photo == null || (photo.IsDeleted && !isAdmin))
        {
            return NotFound();
        }

        if (photo.UserId != currentUserId && !isAdmin)
        {
            return Forbid();
        }

        var primaryWebRoot = !string.IsNullOrWhiteSpace(_storageOptions.Value?.WebRootPath)
            ? _storageOptions.Value.WebRootPath
            : Path.Combine(_env.ContentRootPath, "wwwroot");

        var canonicalRoot = Path.GetFullPath(primaryWebRoot);
        var sanitizedRel = PhotoPathSanitizer.NormalizeToRelativePath(photo.PhotoUri).TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, sanitizedRel));

        if (!fullPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!System.IO.File.Exists(fullPath))
        {
            // Probe fallback wwwroot (e.g. for seeded SVG assets in web root while storage points to /app/data)
            var fallbackWebRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot"));
            var fallbackFullPath = Path.GetFullPath(Path.Combine(fallbackWebRoot, sanitizedRel));
            if (fallbackFullPath.StartsWith(fallbackWebRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fallbackFullPath))
            {
                fullPath = fallbackFullPath;
            }
            else
            {
                return NotFound();
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

        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'");
        Response.Headers.Append("Cache-Control", "private, max-age=3600");

        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }
}

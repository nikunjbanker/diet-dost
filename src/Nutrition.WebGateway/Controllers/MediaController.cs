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
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.ProgressPhotos.Queries.GetProgressPhotoStream;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Thin Authenticated Media Controller for sensitive user progress photos via CQRS dispatcher,
/// enforcing role-based and user-scoped authorization per GAP-05 and DPDPA 2023.
/// </summary>
[Authorize]
[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IWebHostEnvironment _env;

    public MediaController(IDispatcher dispatcher, IWebHostEnvironment env)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    [HttpGet("progress/{id}")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3003:Review code for file path injection vulnerabilities",
        Justification = "Photo URI is retrieved from a verified database entity, sanitized against path traversal, and verified to remain strictly within the web root.")]
    public async Task<IActionResult> GetProgressPhoto(string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId() ?? string.Empty;
        var isAdmin = User.IsInRole(UserRole.Admin.ToString()) || User.IsInRole(UserRole.SuperAdmin.ToString());

        var result = await _dispatcher.QueryAsync(
            new GetProgressPhotoStreamQuery(id, currentUserId, isAdmin, _env.ContentRootPath),
            ct);

        if (!result.Succeeded || result.Data is null)
        {
            return result.StatusCode == 403 ? Forbid() : NotFound();
        }

        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'");
        Response.Headers.Append("Cache-Control", "private, no-store");

        return PhysicalFile(result.Data.FullPath, result.Data.ContentType, enableRangeProcessing: true);
    }
}

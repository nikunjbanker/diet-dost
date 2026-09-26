/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.ProgressPhotos.Commands.DeleteProgressPhoto;
using Nutrition.Application.Features.ProgressPhotos.Commands.UploadProgressPhoto;
using Nutrition.Application.Features.ProgressPhotos.Queries.CompareProgressPhotos;
using Nutrition.Application.Features.ProgressPhotos.Queries.GetProgressPhotos;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Thin Presentation Controller for user progress photos and visual comparisons.
/// Dispatches all persistence, file I/O, and comparison operations to Application CQRS handlers.
/// </summary>
[Authorize]
[ApiController]
[Route("api/progress-photos")]
public class ProgressPhotosController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProgressPhotosController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ImageUploadValidator.MaxSizeBytes)]
    public async Task<IActionResult> UploadProgressPhoto(
        [FromForm] IFormFile? image,
        [FromForm] string? userId,
        [FromForm] double? weightKg,
        [FromForm] ProgressPhotoType photoType = ProgressPhotoType.Face,
        [FromForm] bool isBaseline = false,
        [FromForm] string? notes = null,
        [FromForm] string? capturedDate = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Please provide an image file." });

        using var stream = image.OpenReadStream();
        var (isValid, errorMessage, mimeType) = ImageUploadValidator.ValidateImage(stream, image.Length);
        if (!isValid)
            return BadRequest(new { error = errorMessage });

        var command = new UploadProgressPhotoCommand(
            currentUserId,
            stream,
            image.FileName,
            mimeType!,
            weightKg,
            photoType,
            isBaseline,
            notes,
            capturedDate);

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> GetPhotos(
        [FromQuery] string? userId,
        [FromQuery] ProgressPhotoType? photoType = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetProgressPhotosQuery(
            currentUserId,
            userId,
            User.IsAdminOrSuper(),
            photoType);

        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("comparison")]
    public async Task<IActionResult> GetComparison(
        [FromQuery] string? userId,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var query = new CompareProgressPhotosQuery(
            currentUserId,
            userId,
            userTier,
            User.IsAdminOrSuper());

        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePhoto(string id, CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new DeleteProgressPhotoCommand(
            id,
            currentUserId,
            User.IsAdminOrSuper());

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new { message = "Progress photo deleted successfully." });
    }
}

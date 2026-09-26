/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Analytics.Queries.ExportAnalytics;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Application.Features.Analytics.Queries.GetProjections;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Thin Presentation Controller for Health Analytics & Projections.
/// Dispatches daily ledgers, multi-period trend projections, and exports to Application CQRS handlers.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AnalyticsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyLedger(
        [FromQuery] string? userId,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetDailyLedgerQuery(currentUserId, userId, User.IsAdminOrSuper(), date);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("projections")]
    public async Task<IActionResult> GetProjections(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var query = new GetProjectionsQuery(currentUserId, userId, userTier, User.IsAdminOrSuper(), period);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportData(
        [FromQuery] string? userId,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var query = new ExportAnalyticsDataQuery(currentUserId, userId, userTier, User.IsAdminOrSuper());
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return File(result.Data!.FileBytes, result.Data.ContentType, result.Data.FileName);
    }
}

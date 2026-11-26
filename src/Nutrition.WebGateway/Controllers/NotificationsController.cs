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
using Nutrition.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Nutrition.Application.Features.Notifications.Commands.MarkNotificationRead;
using Nutrition.Application.Features.Notifications.Queries.GetNotifications;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Thin controller for querying and managing in-app notifications via CQRS dispatcher.
/// Role-gated to Admin/SuperAdmin for administrative alerts, or authenticated users for personal alerts.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public NotificationsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken ct = default)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var result = await _dispatcher.QueryAsync(new GetNotificationsQuery(userId, isAdmin), ct);
        if (!result.Succeeded || result.Data is null)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(result.Data);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] string id, CancellationToken ct = default)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var result = await _dispatcher.SendAsync(new MarkNotificationReadCommand(id, userId, isAdmin), ct);
        if (!result.Succeeded)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(new { success = true, id });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var result = await _dispatcher.SendAsync(new MarkAllNotificationsReadCommand(userId, isAdmin), ct);
        if (!result.Succeeded)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(new { success = true, count = result.Data });
    }
}

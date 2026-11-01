/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Controller for querying and managing in-app notifications.
/// Role-gated to Admin/SuperAdmin for administrative alerts, or authenticated users for personal alerts.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly DietTrackerDbContext _db;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        DietTrackerDbContext db,
        ILogger<NotificationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var query = _db.Notifications.AsNoTracking().Where(n => !n.IsDeleted);

        if (isSuperAdmin)
        {
            query = query.Where(n => n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId);
        }
        else
        {
            query = query.Where(n => n.RecipientUserId == userId);
        }

        var list = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        var unreadCount = list.Count(n => !n.IsRead);

        return Ok(new
        {
            unreadCount,
            notifications = list.Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.MetadataJson,
                n.IsRead,
                n.ReadAtUtc,
                n.CreatedAtUtc
            })
        });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] string id, CancellationToken ct = default)
    {
        var notif = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, ct);
        if (notif == null)
            return NotFound(new { error = "Notification not found." });

        notif.IsRead = true;
        notif.ReadAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, id });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var query = _db.Notifications.Where(n => !n.IsDeleted && !n.IsRead);

        if (isSuperAdmin)
        {
            query = query.Where(n => n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId);
        }
        else
        {
            query = query.Where(n => n.RecipientUserId == userId);
        }

        var unread = await query.ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, count = unread.Count });
    }
}


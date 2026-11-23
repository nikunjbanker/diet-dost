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
using Nutrition.Domain.Model.Security;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Controller for querying and managing in-app notifications.
/// Role-gated to Admin/SuperAdmin for administrative alerts, or authenticated users for personal alerts.
/// Decoupled from EF Core via IRepository and IUnitOfWork abstractions (Clean Architecture & DIP).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IRepository<AppNotification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IRepository<AppNotification> notificationRepo,
        IUnitOfWork unitOfWork,
        ILogger<NotificationsController> logger)
    {
        _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        List<AppNotification> list;
        if (isSuperAdmin)
        {
            list = await _notificationRepo.FindAsync(n => n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId, ct);
        }
        else
        {
            list = await _notificationRepo.FindAsync(n => n.RecipientUserId == userId, ct);
        }

        var sortedList = list
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToList();

        var unreadCount = sortedList.Count(n => !n.IsRead);

        return Ok(new
        {
            unreadCount,
            notifications = sortedList.Select(n => new
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
        var notif = await _notificationRepo.GetByIdAsync(id, ct);
        if (notif == null)
            return NotFound(new { error = "Notification not found." });

        notif.IsRead = true;
        notif.ReadAtUtc = DateTime.UtcNow;
        await _notificationRepo.UpdateAsync(notif, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { success = true, id });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var isSuperAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        List<AppNotification> unread;
        if (isSuperAdmin)
        {
            unread = await _notificationRepo.FindAsync(n => !n.IsRead && (n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId), ct);
        }
        else
        {
            unread = await _notificationRepo.FindAsync(n => !n.IsRead && n.RecipientUserId == userId, ct);
        }

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
            await _notificationRepo.UpdateAsync(item, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { success = true, count = unread.Count });
    }
}


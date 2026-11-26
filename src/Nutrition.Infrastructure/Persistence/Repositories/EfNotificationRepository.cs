/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.Repositories;

public sealed class EfNotificationRepository : INotificationRepository
{
    private readonly DietTrackerDbContext _db;

    public EfNotificationRepository(DietTrackerDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<(List<AppNotification> Notifications, int UnreadCount)> GetForRecipientAsync(
        string userId,
        bool isAdminOrSuper,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken ct = default)
    {
        var baseQuery = _db.Set<AppNotification>()
            .AsNoTracking()
            .Where(n => !n.IsDeleted &&
                        (isAdminOrSuper
                            ? (n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId)
                            : n.RecipientUserId == userId));

        var listQuery = unreadOnly ? baseQuery.Where(n => !n.IsRead) : baseQuery;

        var list = await listQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        var unreadCount = await baseQuery.CountAsync(n => !n.IsRead, ct);
        return (list, unreadCount);
    }

    public async Task<NotificationReadResult> MarkAsReadAsync(
        string notificationId,
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        var existing = await _db.Set<AppNotification>().FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, ct);
        if (existing == null)
            return NotificationReadResult.NotFound;

        var isOwner = string.Equals(existing.RecipientUserId, userId, StringComparison.Ordinal);
        var isAdminRecipient = isAdminOrSuper &&
            (string.Equals(existing.RecipientRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(existing.RecipientRole, "Admin", StringComparison.OrdinalIgnoreCase));

        if (!isOwner && !isAdminRecipient)
            return NotificationReadResult.Forbidden;

        existing.IsRead = true;
        existing.ReadAtUtc ??= readAtUtc;
        await _db.SaveChangesAsync(ct);
        return NotificationReadResult.Success;
    }

    public async Task<int> MarkAllAsReadAsync(
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        return await _db.Set<AppNotification>()
            .Where(n => !n.IsDeleted &&
                        !n.IsRead &&
                        (isAdminOrSuper
                            ? (n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == userId)
                            : n.RecipientUserId == userId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAtUtc, readAtUtc), ct);
    }

    public async Task ScrubDeletedUserNotificationsAsync(string userId, CancellationToken ct = default)
    {
        await _db.Set<AppNotification>().Where(n => n.RecipientUserId == userId).ExecuteDeleteAsync(ct);
        await _db.Set<AppNotification>()
            .Where(n => n.SubjectUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubjectUserId, (string?)null), ct);
    }
}

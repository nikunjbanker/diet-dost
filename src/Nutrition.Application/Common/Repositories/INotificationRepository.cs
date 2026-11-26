/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Security;

namespace Nutrition.Application.Common.Repositories;

public enum NotificationReadResult
{
    Success = 0,
    NotFound = 1,
    Forbidden = 2
}

/// <summary>
/// Specialized repository port for bounded notification queries, IDOR-safe mark-read, and bulk operations.
/// </summary>
public interface INotificationRepository
{
    Task<(List<AppNotification> Notifications, int UnreadCount)> GetForRecipientAsync(
        string userId,
        bool isAdminOrSuper,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken ct = default);

    Task<NotificationReadResult> MarkAsReadAsync(
        string notificationId,
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default);

    Task<int> MarkAllAsReadAsync(
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default);

    Task ScrubDeletedUserNotificationsAsync(
        string userId,
        CancellationToken ct = default);
}

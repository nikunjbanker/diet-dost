/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Common;

namespace Nutrition.Domain.Model.Security;

/// <summary>
/// Root aggregate entity representing an in-app persistent notification.
/// Dispatched in real-time over SignalR and persisted for historical / offline retrieval.
/// </summary>
public class AppNotification : ISoftDeletable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Target user identifier (null if role-based notification, e.g. all SuperAdmins).
    /// </summary>
    public string? RecipientUserId { get; set; }

    /// <summary>
    /// Target role (e.g. "SuperAdmin", "Admin") when dispatched to an authorization role.
    /// </summary>
    public string? RecipientRole { get; set; }

    /// <summary>
    /// Notification title / subject.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable notification body text.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Notification categorization type (e.g. "NewRegistration", "AccountActivated", "SystemAlert").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Structured metadata JSON payload (e.g. applicant name, email, mobile, timestamp).
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Whether the notification has been marked as read by the recipient.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp in UTC when the notification was marked as read.
    /// </summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Timestamp in UTC when the notification was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp in UTC when the notification was soft deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }
}


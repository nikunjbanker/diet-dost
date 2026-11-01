/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Domain-level contract for broadcasting and persisting in-app notifications.
/// Bridges application CQRS handlers with database persistence and SignalR real-time hubs.
/// </summary>
public interface IInAppNotificationService
{
    /// <summary>
    /// Persists and broadcasts an alert to the SuperAdmin group when a new user registers in Alpha mode.
    /// </summary>
    Task NotifySuperAdminNewRegistrationAsync(
        string userId,
        string name,
        string email,
        string mobile,
        DateTime createdAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Persists and pushes an approval event directly to the applicant when their account is activated.
    /// </summary>
    Task NotifyUserAccountActivatedAsync(
        string userId,
        string email,
        CancellationToken ct = default);
}


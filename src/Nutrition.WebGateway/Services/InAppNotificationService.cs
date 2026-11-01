/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Hubs;

namespace Nutrition.WebGateway.Services;

/// <summary>
/// Production implementation of IInAppNotificationService.
/// Persists notifications in the database and broadcasts real-time events via SignalR.
/// </summary>
public class InAppNotificationService : IInAppNotificationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(
        IServiceProvider serviceProvider,
        IHubContext<NotificationHub> hubContext,
        ILogger<InAppNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifySuperAdminNewRegistrationAsync(
        string userId,
        string name,
        string email,
        string mobile,
        DateTime createdAtUtc,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        var notification = new AppNotification
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientRole = "SuperAdmin",
            Title = "New Alpha User Registration",
            Message = $"Applicant {name} ({email}, {mobile}) created an account awaiting approval.",
            Type = "NewRegistration",
            MetadataJson = JsonSerializer.Serialize(new
            {
                userId,
                name,
                email,
                mobile,
                createdAtUtc
            }),
            IsRead = false,
            CreatedAtUtc = createdAtUtc
        };

        await db.Notifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("[NOTIFICATIONS] Persisted NewRegistration notification {NotificationId} for applicant {Email}", notification.Id, email);

        // Push real-time SignalR alert to connected SuperAdmins
        try
        {
            await _hubContext.Clients.Group("SuperAdmins").SendAsync("NewRegistration", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                userId,
                name,
                email,
                mobile,
                createdAtUtc = notification.CreatedAtUtc
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NOTIFICATIONS] SignalR broadcast to SuperAdmins failed for applicant {Email}", email);
        }
    }

    public async Task NotifyUserAccountActivatedAsync(
        string userId,
        string email,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        var notification = new AppNotification
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientUserId = userId,
            Title = "Diet-Dost Account Activated",
            Message = $"Your Diet-Dost account ({email}) has been activated by the application owner. You can now log in.",
            Type = "AccountActivated",
            MetadataJson = JsonSerializer.Serialize(new
            {
                userId,
                email,
                activatedAtUtc = DateTime.UtcNow
            }),
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Notifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("[NOTIFICATIONS] Persisted AccountActivated notification {NotificationId} for user {Email}", notification.Id, email);

        // Push real-time SignalR event to all listeners tracking this user's approval
        try
        {
            await _hubContext.Clients.All.SendAsync("AccountApproved", new
            {
                userId,
                email,
                message = "Your Diet-Dost account has been approved and activated! You can now log in."
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NOTIFICATIONS] SignalR push for AccountApproved failed for user {Email}", email);
        }
    }
}


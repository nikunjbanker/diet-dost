/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Application.Features.Notifications.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(string Id, string UserId, bool IsAdminOrSuper) : ICommand<Result<string>>;

public sealed class MarkNotificationReadCommandHandler : ICommandHandler<MarkNotificationReadCommand, Result<string>>
{
    private readonly IRepository<AppNotification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationReadCommandHandler(IRepository<AppNotification> notificationRepo, IUnitOfWork unitOfWork)
    {
        _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<string>> HandleAsync(MarkNotificationReadCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<string>.Unauthorized();

        var notif = await _notificationRepo.GetByIdAsync(request.Id, ct);
        if (notif == null)
            return Result<string>.NotFound("Notification not found.");

        // IDOR Guard: caller must own the notification or hold an admin role for admin-scoped notifications
        var isOwner = string.Equals(notif.RecipientUserId, request.UserId, StringComparison.Ordinal);
        var isAdminRecipient = request.IsAdminOrSuper &&
            (string.Equals(notif.RecipientRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(notif.RecipientRole, "Admin", StringComparison.OrdinalIgnoreCase));

        if (!isOwner && !isAdminRecipient)
            return Result<string>.Forbidden("Access denied to modify another user's notification.");

        notif.IsRead = true;
        notif.ReadAtUtc ??= DateTime.UtcNow;
        await _notificationRepo.UpdateAsync(notif, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<string>.Success(notif.Id);
    }
}

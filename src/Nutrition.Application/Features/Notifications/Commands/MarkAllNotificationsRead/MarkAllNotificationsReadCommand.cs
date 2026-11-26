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

namespace Nutrition.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand(string UserId, bool IsAdminOrSuper) : ICommand<Result<int>>;

public sealed class MarkAllNotificationsReadCommandHandler : ICommandHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    private readonly IRepository<AppNotification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkAllNotificationsReadCommandHandler(IRepository<AppNotification> notificationRepo, IUnitOfWork unitOfWork)
    {
        _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<int>> HandleAsync(MarkAllNotificationsReadCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<int>.Unauthorized();

        var spec = new QuerySpec<AppNotification>(
            Filter: request.IsAdminOrSuper
                ? n => !n.IsRead && (n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == request.UserId)
                : n => !n.IsRead && n.RecipientUserId == request.UserId,
            OrderBy: n => n.CreatedAtUtc,
            Descending: true,
            Take: 200);

        var unread = await _notificationRepo.FindAsync(spec, ct);
        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
            await _notificationRepo.UpdateAsync(item, ct);
        }

        if (unread.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Result<int>.Success(unread.Count);
    }
}

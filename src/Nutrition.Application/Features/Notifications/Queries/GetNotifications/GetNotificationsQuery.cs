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
using Nutrition.Application.Features.Notifications.DTOs;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Application.Features.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery(string UserId, bool IsAdminOrSuper) : IQuery<Result<NotificationFeedDto>>;

public sealed class GetNotificationsQueryHandler : IQueryHandler<GetNotificationsQuery, Result<NotificationFeedDto>>
{
    private readonly IReadRepository<AppNotification> _notificationRepo;

    public GetNotificationsQueryHandler(IReadRepository<AppNotification> notificationRepo)
    {
        _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
    }

    public async Task<Result<NotificationFeedDto>> HandleAsync(GetNotificationsQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<NotificationFeedDto>.Unauthorized();

        var spec = new QuerySpec<AppNotification>(
            Filter: request.IsAdminOrSuper
                ? n => n.RecipientRole == "SuperAdmin" || n.RecipientRole == "Admin" || n.RecipientUserId == request.UserId
                : n => n.RecipientUserId == request.UserId,
            OrderBy: n => n.CreatedAtUtc,
            Descending: true,
            Take: 50);

        var sortedList = await _notificationRepo.FindAsync(spec, ct);
        var unreadCount = sortedList.Count(n => !n.IsRead);

        return Result<NotificationFeedDto>.Success(new NotificationFeedDto(
            unreadCount,
            sortedList.Select(n => n.ToDto()).ToList()));
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Security;

namespace Nutrition.Application.Features.Notifications.DTOs;

public sealed record NotificationDto(
    string Id,
    string Title,
    string Message,
    string Type,
    string? MetadataJson,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
);

public sealed record NotificationFeedDto(
    int UnreadCount,
    List<NotificationDto> Notifications
);

public static class NotificationDtoMapper
{
    public static NotificationDto ToDto(this AppNotification entity) => new(
        entity.Id,
        entity.Title,
        entity.Message,
        entity.Type,
        entity.MetadataJson,
        entity.IsRead,
        entity.ReadAtUtc,
        entity.CreatedAtUtc
    );
}

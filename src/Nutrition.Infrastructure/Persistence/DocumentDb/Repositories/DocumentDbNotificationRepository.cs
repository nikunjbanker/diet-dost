/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Driver;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.DocumentDb.Repositories;

public sealed class DocumentDbNotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<AppNotification> _collection;

    public DocumentDbNotificationRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        DocumentDbBsonMappings.Register();
        _collection = database.GetCollection<AppNotification>("AppNotifications");
    }

    public async Task<(List<AppNotification> Notifications, int UnreadCount)> GetForRecipientAsync(
        string userId,
        bool isAdminOrSuper,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken ct = default)
    {
        var recipientFilter = BuildRecipientFilter(userId, isAdminOrSuper);
        var listFilter = unreadOnly
            ? Builders<AppNotification>.Filter.And(recipientFilter, Builders<AppNotification>.Filter.Eq(n => n.IsRead, false))
            : recipientFilter;

        var unreadFilter = Builders<AppNotification>.Filter.And(
            recipientFilter,
            Builders<AppNotification>.Filter.Eq(n => n.IsRead, false));

        var listTask = _collection.Find(listFilter)
            .Sort(Builders<AppNotification>.Sort.Descending(n => n.CreatedAtUtc))
            .Limit(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        var unreadCountTask = _collection.CountDocumentsAsync(unreadFilter, cancellationToken: ct);
        await Task.WhenAll(listTask, unreadCountTask);

        return (await listTask, (int)await unreadCountTask);
    }

    public async Task<NotificationReadResult> MarkAsReadAsync(
        string notificationId,
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        var existing = await _collection.Find(Builders<AppNotification>.Filter.Eq("_id", notificationId)).FirstOrDefaultAsync(ct);
        if (existing == null || existing.IsDeleted)
            return NotificationReadResult.NotFound;

        var isOwner = string.Equals(existing.RecipientUserId, userId, StringComparison.Ordinal);
        var isAdminRecipient = isAdminOrSuper &&
            (string.Equals(existing.RecipientRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(existing.RecipientRole, "Admin", StringComparison.OrdinalIgnoreCase));

        if (!isOwner && !isAdminRecipient)
            return NotificationReadResult.Forbidden;

        var update = Builders<AppNotification>.Update
            .Set(n => n.IsRead, true)
            .Set(n => n.ReadAtUtc, existing.ReadAtUtc ?? readAtUtc);

        await _collection.UpdateOneAsync(Builders<AppNotification>.Filter.Eq("_id", notificationId), update, cancellationToken: ct);
        return NotificationReadResult.Success;
    }

    public async Task<int> MarkAllAsReadAsync(
        string userId,
        bool isAdminOrSuper,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        var filter = Builders<AppNotification>.Filter.And(
            BuildRecipientFilter(userId, isAdminOrSuper),
            Builders<AppNotification>.Filter.Eq(n => n.IsRead, false));

        var update = Builders<AppNotification>.Update
            .Set(n => n.IsRead, true)
            .Set(n => n.ReadAtUtc, readAtUtc);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task ScrubDeletedUserNotificationsAsync(string userId, CancellationToken ct = default)
    {
        var deletePersonal = _collection.DeleteManyAsync(
            Builders<AppNotification>.Filter.Eq(n => n.RecipientUserId, userId), ct);

        var scrubSubject = _collection.UpdateManyAsync(
            Builders<AppNotification>.Filter.Eq(n => n.SubjectUserId, userId),
            Builders<AppNotification>.Update.Set(n => n.SubjectUserId, null),
            cancellationToken: ct);

        await Task.WhenAll(deletePersonal, scrubSubject);
    }

    private static FilterDefinition<AppNotification> BuildRecipientFilter(string userId, bool isAdminOrSuper)
    {
        var notDeleted = Builders<AppNotification>.Filter.Ne(n => n.IsDeleted, true);
        var scope = isAdminOrSuper
            ? Builders<AppNotification>.Filter.Or(
                Builders<AppNotification>.Filter.Eq(n => n.RecipientRole, "SuperAdmin"),
                Builders<AppNotification>.Filter.Eq(n => n.RecipientRole, "Admin"),
                Builders<AppNotification>.Filter.Eq(n => n.RecipientUserId, userId))
            : Builders<AppNotification>.Filter.Eq(n => n.RecipientUserId, userId);

        return Builders<AppNotification>.Filter.And(notDeleted, scope);
    }
}

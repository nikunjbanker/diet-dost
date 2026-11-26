/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Driver;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Infrastructure.Persistence.DocumentDb.Repositories;

public sealed class DocumentDbQrLoginSessionRepository : IQrLoginSessionRepository
{
    private readonly IMongoCollection<QrLoginSession> _collection;

    public DocumentDbQrLoginSessionRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        DocumentDbBsonMappings.Register();
        _collection = database.GetCollection<QrLoginSession>("QrSessions");
    }

    public async Task<QrLoginSession?> TryMarkScannedAsync(string sessionId, DateTime utcNow, CancellationToken ct = default)
    {
        var filter = Builders<QrLoginSession>.Filter.And(
            Builders<QrLoginSession>.Filter.Eq("_id", sessionId),
            Builders<QrLoginSession>.Filter.Eq(s => s.Status, QrSessionStatus.Pending),
            Builders<QrLoginSession>.Filter.Gt(s => s.ExpiresAtUtc, utcNow));

        var update = Builders<QrLoginSession>.Update
            .Set(s => s.Status, QrSessionStatus.Scanned)
            .Inc(s => s.ConcurrencyVersion, 1);

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QrLoginSession> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<QrLoginSession?> TryApproveAsync(
        string sessionId,
        string userId,
        string exchangeTicket,
        string? mobilePublicKey,
        string? encryptedApprovalPayload,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var filter = Builders<QrLoginSession>.Filter.And(
            Builders<QrLoginSession>.Filter.Eq("_id", sessionId),
            Builders<QrLoginSession>.Filter.In(s => s.Status, new[] { QrSessionStatus.Pending, QrSessionStatus.Scanned }),
            Builders<QrLoginSession>.Filter.Gt(s => s.ExpiresAtUtc, utcNow));

        var update = Builders<QrLoginSession>.Update
            .Set(s => s.Status, QrSessionStatus.Approved)
            .Set(s => s.AuthorizedUserId, userId)
            .Set(s => s.ExchangeTicket, exchangeTicket)
            .Set(s => s.MobilePublicKey, mobilePublicKey)
            .Set(s => s.EncryptedApprovalPayload, encryptedApprovalPayload)
            .Set(s => s.ApprovedAtUtc, utcNow)
            .Inc(s => s.ConcurrencyVersion, 1);

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QrLoginSession> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<QrLoginSession?> TryConsumeAsync(string sessionId, string exchangeTicket, DateTime utcNow, CancellationToken ct = default)
    {
        var filter = Builders<QrLoginSession>.Filter.And(
            Builders<QrLoginSession>.Filter.Eq("_id", sessionId),
            Builders<QrLoginSession>.Filter.Eq(s => s.Status, QrSessionStatus.Approved),
            Builders<QrLoginSession>.Filter.Eq(s => s.ExchangeTicket, exchangeTicket),
            Builders<QrLoginSession>.Filter.Gt(s => s.ExpiresAtUtc, utcNow));

        var update = Builders<QrLoginSession>.Update
            .Set(s => s.Status, QrSessionStatus.Consumed)
            .Set(s => s.ConsumedAtUtc, utcNow)
            .Inc(s => s.ConcurrencyVersion, 1);

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QrLoginSession> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<QrLoginSession?> TryRejectAsync(string sessionId, DateTime utcNow, CancellationToken ct = default)
    {
        var filter = Builders<QrLoginSession>.Filter.And(
            Builders<QrLoginSession>.Filter.Eq("_id", sessionId),
            Builders<QrLoginSession>.Filter.In(s => s.Status, new[] { QrSessionStatus.Pending, QrSessionStatus.Scanned }),
            Builders<QrLoginSession>.Filter.Gt(s => s.ExpiresAtUtc, utcNow));

        var update = Builders<QrLoginSession>.Update
            .Set(s => s.Status, QrSessionStatus.Rejected)
            .Inc(s => s.ConcurrencyVersion, 1);

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<QrLoginSession> { ReturnDocument = ReturnDocument.After },
            ct);
    }
}

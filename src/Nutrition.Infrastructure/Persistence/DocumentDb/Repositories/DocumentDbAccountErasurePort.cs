/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver;
using Nutrition.Application.Common.Repositories;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.DocumentDb.Repositories;

public sealed class DocumentDbAccountErasurePort : IAccountErasurePort
{
    private readonly IMongoDatabase _database;

    public DocumentDbAccountErasurePort(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        DocumentDbBsonMappings.Register();
    }

    public async Task EraseUserAccountAsync(string userId, DateTime erasedAtUtc, CancellationToken ct = default)
    {
        var pseudoUserId = $"anon_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))[..12].ToLowerInvariant()}";

        var tMeals = _database.GetCollection<MealLog>("Meals").UpdateManyAsync(
            Builders<MealLog>.Filter.Eq(x => x.UserId, userId),
            Builders<MealLog>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAtUtc, erasedAtUtc),
            cancellationToken: ct);

        var tLedgers = _database.GetCollection<DailyCalorieLedger>("Ledgers").UpdateManyAsync(
            Builders<DailyCalorieLedger>.Filter.Eq(x => x.UserId, userId),
            Builders<DailyCalorieLedger>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAtUtc, erasedAtUtc),
            cancellationToken: ct);

        var tPhotos = _database.GetCollection<ProgressPhoto>("ProgressPhotos").UpdateManyAsync(
            Builders<ProgressPhoto>.Filter.Eq(x => x.UserId, userId),
            Builders<ProgressPhoto>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAtUtc, erasedAtUtc),
            cancellationToken: ct);

        var tFeedbacks = _database.GetCollection<AiDetectionFeedbackRecord>("AiFeedbacks").UpdateManyAsync(
            Builders<AiDetectionFeedbackRecord>.Filter.Eq(x => x.UserId, userId),
            Builders<AiDetectionFeedbackRecord>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAtUtc, erasedAtUtc),
            cancellationToken: ct);

        var tCorrections = _database.GetCollection<UserCorrectionRecord>("Corrections").UpdateManyAsync(
            Builders<UserCorrectionRecord>.Filter.Eq(x => x.UserId, userId),
            Builders<UserCorrectionRecord>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAtUtc, erasedAtUtc),
            cancellationToken: ct);

        var tOtps = _database.GetCollection<VerificationOtp>("VerificationOtps").DeleteManyAsync(
            Builders<VerificationOtp>.Filter.Eq(x => x.UserId, userId), ct);

        var tQr = _database.GetCollection<QrLoginSession>("QrSessions").DeleteManyAsync(
            Builders<QrLoginSession>.Filter.Eq(x => x.AuthorizedUserId, userId), ct);

        var tNotifs = _database.GetCollection<AppNotification>("AppNotifications").DeleteManyAsync(
            Builders<AppNotification>.Filter.Eq(x => x.RecipientUserId, userId), ct);

        var tAiLogs = _database.GetCollection<AiUsageLog>("AiUsageLogs").UpdateManyAsync(
            Builders<AiUsageLog>.Filter.Eq(x => x.UserId, userId),
            Builders<AiUsageLog>.Update.Set(x => x.UserId, pseudoUserId),
            cancellationToken: ct);

        await Task.WhenAll(tMeals, tLedgers, tPhotos, tFeedbacks, tCorrections, tOtps, tQr, tNotifs, tAiLogs);
    }
}

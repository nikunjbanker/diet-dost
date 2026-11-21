/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence.DocumentDb;

/// <summary>
/// Registers authoritative BSON class maps for all 14 domain entities,
/// mapping primary keys to string GUID _id fields, enforcing universal UTC DateTime serialization,
/// and allowing forward-compatible schema evolution.
/// </summary>
public static class DocumentDbBsonMappings
{
    private static readonly object SyncLock = new();
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered) return;

        lock (SyncLock)
        {
            if (_isRegistered) return;

            // Universal UTC DateTime Serializer
            try
            {
                BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
            }
            catch
            {
                // Serializer may already be registered by Mongo driver or test harness
            }

            try
            {
                BsonSerializer.RegisterSerializer(typeof(DateTime?), new NullableSerializer<DateTime>(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime)));
            }
            catch
            {
                // Already registered
            }

            // 1. UserProfile
            RegisterClassMapWithId<UserProfile>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 2. MealLog
            RegisterClassMapWithId<MealLog>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 3. FoodItemRecord
            RegisterClassMapWithId<FoodItemRecord>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 4. DailyCalorieLedger
            RegisterClassMapWithId<DailyCalorieLedger>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 5. UserCorrectionRecord
            RegisterClassMapWithId<UserCorrectionRecord>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 6. ProgressPhoto
            RegisterClassMapWithId<ProgressPhoto>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 7. AiDetectionFeedbackRecord
            RegisterClassMapWithId<AiDetectionFeedbackRecord>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 8. ApplicationUser
            RegisterClassMapWithId<ApplicationUser>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 9. VerificationOtp
            RegisterClassMapWithId<VerificationOtp>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 10. TierFeatureConfiguration
            RegisterClassMapWithId<TierFeatureConfiguration>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 11. AiUsageLog
            RegisterClassMapWithId<AiUsageLog>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 12. AppSecret (Uses 'Key' as primary key)
            RegisterClassMapWithId<AppSecret>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Key)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 13. QrLoginSession
            RegisterClassMapWithId<QrLoginSession>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            // 14. AppNotification
            RegisterClassMapWithId<AppNotification>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });

            _isRegistered = true;
        }
    }

    private static void RegisterClassMapWithId<T>(Action<BsonClassMap<T>> configure)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap<T>(configure);
        }
    }
}

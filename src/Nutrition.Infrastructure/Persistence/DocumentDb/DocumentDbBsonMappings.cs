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

            // Universal Resilient DateOnly Serializers (handles legacy timezone drifts and ISO strings)
            try
            {
                BsonSerializer.RegisterSerializer(typeof(DateOnly), new ResilientDateOnlySerializer());
            }
            catch
            {
                // Serializer may already be registered
            }

            try
            {
                BsonSerializer.RegisterSerializer(typeof(DateOnly?), new NullableSerializer<DateOnly>(new ResilientDateOnlySerializer()));
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
                cm.MapMember(c => c.Date).SetSerializer(new ResilientDateOnlySerializer());
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

/// <summary>
/// Resilient BSON serializer for DateOnly that safely handles both pure dates and DateTimes with non-zero time components
/// (e.g., from UTC timezone offsets, legacy migrations, or ISO-8601 string representations) without throwing FormatException.
/// </summary>
public sealed class ResilientDateOnlySerializer : SerializerBase<DateOnly>
{
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();
        switch (bsonType)
        {
            case BsonType.DateTime:
                var millis = context.Reader.ReadDateTime();
                var dt = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(millis);
                return DateOnly.FromDateTime(dt.Date);

            case BsonType.String:
                var str = context.Reader.ReadString();
                if (DateOnly.TryParse(str, out var parsedDateOnly))
                {
                    return parsedDateOnly;
                }
                if (DateTime.TryParse(str, out var parsedDt))
                {
                    return DateOnly.FromDateTime(parsedDt.Date);
                }
                return DateOnly.MinValue;

            case BsonType.Int32:
                var intVal = context.Reader.ReadInt32();
                if (intVal > 19000101 && intVal < 21001231)
                {
                    var y = intVal / 10000;
                    var m = (intVal % 10000) / 100;
                    var d = intVal % 100;
                    return new DateOnly(y, m, d);
                }
                return DateOnly.FromDayNumber(intVal);

            case BsonType.Int64:
                var longMillis = context.Reader.ReadInt64();
                var longDt = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(longMillis);
                return DateOnly.FromDateTime(longDt.Date);

            case BsonType.Null:
                context.Reader.ReadNull();
                return default;

            default:
                throw new FormatException($"Cannot deserialize DateOnly from BsonType '{bsonType}'.");
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
    {
        var dt = DateTime.SpecifyKind(value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        context.Writer.WriteDateTime(BsonUtils.ToMillisecondsSinceEpoch(dt));
    }
}

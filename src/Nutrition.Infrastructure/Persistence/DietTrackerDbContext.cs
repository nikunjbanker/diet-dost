/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;

namespace Nutrition.Infrastructure.Persistence;

public class DietTrackerDbContext : DbContext
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<MealLog> Meals => Set<MealLog>();
    public DbSet<FoodItemRecord> FoodItems => Set<FoodItemRecord>();
    public DbSet<DailyCalorieLedger> Ledgers => Set<DailyCalorieLedger>();
    public DbSet<UserCorrectionRecord> Corrections => Set<UserCorrectionRecord>();
    public DbSet<ProgressPhoto> ProgressPhotos => Set<ProgressPhoto>();
    public DbSet<AiDetectionFeedbackRecord> AiFeedbacks => Set<AiDetectionFeedbackRecord>();

    // Identity & Security DbSets
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<VerificationOtp> VerificationOtps => Set<VerificationOtp>();
    public DbSet<TierFeatureConfiguration> TierConfigurations => Set<TierFeatureConfiguration>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AppSecret> AppSecrets => Set<AppSecret>();

    public DietTrackerDbContext(DbContextOptions<DietTrackerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Reusable ValueComparers for collections to guarantee proper EF Core change tracking without data loss or warnings
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c != null ? c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())) : 0,
            c => c != null ? c.ToList() : new List<string>());

        var medicationListComparer = new ValueComparer<List<MedicationEntry>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null)),
            c => c != null ? JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode() : 0,
            c => c != null ? JsonSerializer.Deserialize<List<MedicationEntry>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new List<MedicationEntry>() : new List<MedicationEntry>());

        // UserProfile JSON converters with ValueComparers
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiagnosedConditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    stringListComparer);

            entity.Property(e => e.Medications)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<MedicationEntry>>(v, (JsonSerializerOptions?)null) ?? new List<MedicationEntry>(),
                    medicationListComparer);
        });

        // MealLog configuration with AutoInclude for Items and ValueComparers
        modelBuilder.Entity<MealLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Items)
                  .WithOne()
                  .HasForeignKey(i => i.MealLogId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Items).AutoInclude();

            entity.Property(e => e.WhoComplianceFlags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    stringListComparer);

            entity.Property(e => e.MedicationWarnings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    stringListComparer);
        });

        // FoodItemRecord
        modelBuilder.Entity<FoodItemRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // UserCorrectionRecord (Continuous model training & memory)
        modelBuilder.Entity<UserCorrectionRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.OriginalDetectedItem });
        });

        // DailyCalorieLedger JSON converters with ValueComparer
        modelBuilder.Entity<DailyCalorieLedger>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EarnedBadges)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    stringListComparer);
        });

        // ProgressPhoto entity configuration
        modelBuilder.Entity<ProgressPhoto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CapturedAtUtc });
            entity.HasIndex(e => new { e.UserId, e.PhotoType });
        });

        // AiDetectionFeedbackRecord entity configuration
        modelBuilder.Entity<AiDetectionFeedbackRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc });
            entity.HasIndex(e => e.Rating);
        });

        // ApplicationUser entity configuration
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            entity.HasIndex(e => e.NormalizedMobileNumber);
            entity.HasIndex(e => e.Role);
            entity.HasIndex(e => e.Tier);
        });

        // VerificationOtp entity configuration
        modelBuilder.Entity<VerificationOtp>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Target });
            entity.HasIndex(e => new { e.Target, e.Channel, e.IsUsed });
            entity.HasIndex(e => e.ExpiresAtUtc);
        });

        // TierFeatureConfiguration entity configuration
        modelBuilder.Entity<TierFeatureConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Tier).IsUnique();
        });

        // AiUsageLog entity configuration
        modelBuilder.Entity<AiUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.TimestampUtc });
            entity.HasIndex(e => new { e.UserId, e.OperationType });
        });

        modelBuilder.Entity<AppSecret>(entity =>
        {
            entity.ToTable("AppSecrets");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        // ============================================================================
        // Universal UTC Date Storage Standard
        // Guarantees all DateTime properties are saved as UTC in SQLite and read back as DateTimeKind.Utc
        // ============================================================================
        var utcDateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var utcNullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
            v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcDateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcNullableDateTimeConverter);
                }
            }
        }
    }
}

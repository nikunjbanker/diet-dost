using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;

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
    }
}

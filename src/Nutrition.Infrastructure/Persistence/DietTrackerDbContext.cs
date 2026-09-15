using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

    public DietTrackerDbContext(DbContextOptions<DietTrackerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UserProfile JSON converters
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiagnosedConditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property(e => e.Medications)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<MedicationEntry>>(v, (JsonSerializerOptions?)null) ?? new List<MedicationEntry>());
        });

        // MealLog configuration with AutoInclude for Items
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
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property(e => e.MedicationWarnings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
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

        // DailyCalorieLedger JSON converters
        modelBuilder.Entity<DailyCalorieLedger>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EarnedBadges)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        });

        // ProgressPhoto entity configuration
        modelBuilder.Entity<ProgressPhoto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CapturedAtUtc });
            entity.HasIndex(e => new { e.UserId, e.PhotoType });
        });
    }
}

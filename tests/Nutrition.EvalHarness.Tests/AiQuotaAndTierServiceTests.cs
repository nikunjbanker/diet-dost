using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class AiQuotaAndTierServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly TierConfigurationService _tierConfigService;
    private readonly AiQuotaService _quotaService;

    public AiQuotaAndTierServiceTests()
    {
        TierConfigurationService.ClearCache();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        // Seed default tier configurations
        _db.TierConfigurations.AddRange(TierFeatureConfiguration.GetDefaultConfigurations());
        _db.SaveChanges();

        _tierConfigService = new TierConfigurationService(_db);
        _quotaService = new AiQuotaService(_db, _tierConfigService);
    }

    public void Dispose()
    {
        TierConfigurationService.ClearCache();
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task FreeTier_AllowsOneDetection_BlocksSecondAttempt()
    {
        var userId = "user-free-01";
        var timezone = "Asia/Kolkata";

        // Initial check - should be allowed (0 used out of 1)
        var initial = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, timezone);
        Assert.True(initial.IsAllowed);
        Assert.Equal(1, initial.DailyLimit);
        Assert.Equal(0, initial.UsedToday);
        Assert.Equal(1, initial.RemainingCalls);

        // Record first detection
        await _quotaService.RecordUsageAsync(
            userId,
            AiOperationType.PhotoDetection,
            "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 800,
            isSuccess: true
        );

        // Second check - must be blocked (1 used out of 1)
        var second = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, timezone);
        Assert.False(second.IsAllowed);
        Assert.Equal(1, second.DailyLimit);
        Assert.Equal(1, second.UsedToday);
        Assert.Equal(0, second.RemainingCalls);
        Assert.Contains("limit reached", second.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BasicTier_AllowsSevenDetections_BlocksEighthAttempt()
    {
        var userId = "user-basic-01";
        var timezone = "Asia/Kolkata";

        for (int i = 0; i < 7; i++)
        {
            var check = await _quotaService.CheckQuotaAsync(userId, UserTier.Basic, timezone);
            Assert.True(check.IsAllowed);

            await _quotaService.RecordUsageAsync(
                userId,
                AiOperationType.TextDetection,
                "Gemini-3.8-Flash",
                estimatedTokens: 600,
                latencyMs: 500,
                isSuccess: true
            );
        }

        // 8th attempt must be rejected
        var eighth = await _quotaService.CheckQuotaAsync(userId, UserTier.Basic, timezone);
        Assert.False(eighth.IsAllowed);
        Assert.Equal(7, eighth.DailyLimit);
        Assert.Equal(7, eighth.UsedToday);
        Assert.Equal(0, eighth.RemainingCalls);
    }

    [Fact]
    public async Task PremiumTier_AllowsThirtyDetections()
    {
        var userId = "user-premium-01";
        var timezone = "Asia/Kolkata";

        // Record 29 operations
        for (int i = 0; i < 29; i++)
        {
            await _quotaService.RecordUsageAsync(
                userId,
                AiOperationType.PhotoDetection,
                "Gemini-3.8-Flash",
                estimatedTokens: 1200,
                latencyMs: 900,
                isSuccess: true
            );
        }

        // 30th call allowed with 1 remaining
        var check29 = await _quotaService.CheckQuotaAsync(userId, UserTier.Premium, timezone);
        Assert.True(check29.IsAllowed);
        Assert.Equal(30, check29.DailyLimit);
        Assert.Equal(29, check29.UsedToday);
        Assert.Equal(1, check29.RemainingCalls);

        // Record 30th
        await _quotaService.RecordUsageAsync(
            userId,
            AiOperationType.PhotoDetection,
            "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 900,
            isSuccess: true
        );

        // 31st call blocked
        var check31 = await _quotaService.CheckQuotaAsync(userId, UserTier.Premium, timezone);
        Assert.False(check31.IsAllowed);
        Assert.Equal(0, check31.RemainingCalls);
    }

    [Fact]
    public async Task SuperAdmin_HasInfiniteQuota()
    {
        var userId = "user-god-01";
        var timezone = "Asia/Kolkata";

        for (int i = 0; i < 35; i++)
        {
            await _quotaService.RecordUsageAsync(
                userId,
                AiOperationType.PhotoDetection,
                "Gemini-3.8-Flash",
                estimatedTokens: 1200,
                latencyMs: 500,
                isSuccess: true
            );
        }

        var check = await _quotaService.CheckQuotaAsync(userId, UserTier.SuperAdmin, timezone);
        Assert.True(check.IsAllowed);
        Assert.Equal(-1, check.DailyLimit);
        Assert.Equal(35, check.UsedToday);
    }

    [Fact]
    public async Task TierConfigurationService_UpdatesRuntimeLimitsAndInvalidatesCache()
    {
        // Get initial Free tier config (limit: 1)
        var initial = await _tierConfigService.GetConfigurationAsync(UserTier.Free);
        Assert.Equal(1, initial.DailyAiDetectionLimit);

        // Update limit dynamically to 5
        initial.DailyAiDetectionLimit = 5;
        initial.AllowPhotoCompare = true;
        await _tierConfigService.UpdateConfigurationAsync(initial);

        // Query again - should reflect updated configuration
        var updated = await _tierConfigService.GetConfigurationAsync(UserTier.Free);
        Assert.Equal(5, updated.DailyAiDetectionLimit);
        Assert.True(updated.AllowPhotoCompare);
    }

    [Fact]
    public async Task GetUsageStatsAsync_ReturnsAccurateRollups()
    {
        var userId = "user-stats-01";
        var timezone = "Asia/Kolkata";

        await _quotaService.RecordUsageAsync(userId, AiOperationType.PhotoDetection, "Gemini", 1000, 500, isSuccess: true);
        await _quotaService.RecordUsageAsync(userId, AiOperationType.TextDetection, "Gemini", 500, 300, isSuccess: true);

        var stats = await _quotaService.GetUsageStatsAsync(userId, UserTier.Basic, timezone);
        Assert.Equal(2, stats.UsedToday);
        Assert.Equal(2, stats.UsedLast7Days);
        Assert.Equal(2, stats.UsedLast30Days);
        Assert.Equal(5, stats.RemainingCalls);
        Assert.Equal(2, stats.RecentOperations.Count);
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Basic)]
    public async Task FreeAndBasicTiers_MustHavePhotoCompareDisabledByDefault(UserTier tier)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier);
        Assert.False(config.AllowPhotoCompare, $"PhotoCompare must be disabled for tier {tier}");
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Basic)]
    public async Task FreeAndBasicTiers_MustHaveDataExportDisabledByDefault(UserTier tier)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier);
        Assert.False(config.AllowDataExport, $"DataExport must be disabled for tier {tier}");
    }

    [Theory]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.SuperAdmin)]
    public async Task PremiumAndSuperAdminTiers_MustHavePhotoCompareAndDataExportEnabled(UserTier tier)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier);
        Assert.True(config.AllowPhotoCompare, $"PhotoCompare must be enabled for tier {tier}");
        Assert.True(config.AllowDataExport, $"DataExport must be enabled for tier {tier}");
    }

    [Fact]
    public async Task PhotoDetectionUsage_IncrementsQuotaCountCorrectly()
    {
        var userId = "user-photo-quota";
        var timezone = "Asia/Kolkata";

        // Check quota initially
        var initial = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, timezone);
        Assert.True(initial.IsAllowed);
        Assert.Equal(0, initial.UsedToday);

        // Record photo detection operation
        await _quotaService.RecordUsageAsync(
            userId,
            AiOperationType.PhotoDetection,
            "Gemini-3.8-Flash",
            estimatedTokens: 1200,
            latencyMs: 1100,
            isSuccess: true);

        // Check quota after photo operation
        var after = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, timezone);
        Assert.False(after.IsAllowed);
        Assert.Equal(1, after.UsedToday);
        Assert.Equal(0, after.RemainingCalls);
    }

    [Theory]
    [InlineData(UserTier.Free, 7)]
    [InlineData(UserTier.Basic, 30)]
    [InlineData(UserTier.Premium, 365)]
    [InlineData(UserTier.SuperAdmin, 365)]
    public async Task AnalyticsHistoryDays_MustMatchTierSpecification(UserTier tier, int expectedDays)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier);
        Assert.Equal(expectedDays, config.AnalyticsHistoryDays);
    }
}

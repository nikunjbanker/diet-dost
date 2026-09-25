using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

[Collection("TierConfigTests")]
public class TierFunctionalityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly TierConfigurationService _tierConfigService;
    private readonly AiQuotaService _quotaService;
    private readonly MicrosoftAgentFoodVisionService _visionService;

    public TierFunctionalityTests()
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

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AI:ModelId", "gemini-3.8-flash" },
                { "AI:Endpoint", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" }
            })
            .Build();

        _visionService = new MicrosoftAgentFoodVisionService(
            config,
            NullLogger<MicrosoftAgentFoodVisionService>.Instance,
            new HttpClient());
    }

    public void Dispose()
    {
        TierConfigurationService.ClearCache();
        _db.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(UserTier.Free, 1, false, false, 7)]
    [InlineData(UserTier.Basic, 7, false, false, 30)]
    [InlineData(UserTier.Premium, 30, true, true, 365)]
    [InlineData(UserTier.SuperAdmin, -1, true, true, 365)]
    public async Task Validate_TierConfigurations_EnforceExpectedPolicyMatrix(
        UserTier tier, int expectedLimit, bool expectedPhotoCompare, bool expectedDataExport, int expectedHistoryDays)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier);

        Assert.NotNull(config);
        Assert.Equal(expectedLimit, config.DailyAiDetectionLimit);
        Assert.Equal(expectedPhotoCompare, config.AllowPhotoCompare);
        Assert.Equal(expectedDataExport, config.AllowDataExport);
        Assert.Equal(expectedHistoryDays, config.AnalyticsHistoryDays);
    }

    [Fact]
    public async Task FreeTier_QuotaGating_EnforcesDailyLimitAndRejection()
    {
        const string userId = "free-demo-user";
        const string tz = "Asia/Kolkata";

        // Initial call should be permitted
        var quota1 = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, tz);
        Assert.True(quota1.IsAllowed);
        Assert.Equal(1, quota1.DailyLimit);
        Assert.Equal(1, quota1.RemainingCalls);

        // Record usage
        await _quotaService.RecordUsageAsync(userId, AiOperationType.PhotoDetection, "Gemini", 1200, 450, true);

        // Subsequent call must be blocked
        var quota2 = await _quotaService.CheckQuotaAsync(userId, UserTier.Free, tz);
        Assert.False(quota2.IsAllowed);
        Assert.Equal(0, quota2.RemainingCalls);
        Assert.Contains("limit reached", quota2.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuperAdminTier_QuotaGating_AlwaysPermitsUnboundedCalls()
    {
        const string userId = "superadmin-demo-user";
        const string tz = "Asia/Kolkata";

        // Simulate 50 calls
        for (int i = 0; i < 50; i++)
        {
            await _quotaService.RecordUsageAsync(userId, AiOperationType.PhotoDetection, "Gemini", 1200, 400, true);
        }

        var quota = await _quotaService.CheckQuotaAsync(userId, UserTier.SuperAdmin, tz);
        Assert.True(quota.IsAllowed);
        Assert.Equal(-1, quota.DailyLimit);
        Assert.Equal(50, quota.UsedToday);
        Assert.Null(quota.RejectionReason);
    }

    [Fact]
    public async Task FoodVisionService_MealTypeAware_IdentifiesBreakfastItems()
    {
        byte[] bytes = new byte[1024];
        using var stream = new MemoryStream(bytes);
        var profile = new UserProfile { Name = "Breakfast User", RegionalCuisine = "Maharashtra" };

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream, "image/jpeg", "Maharashtra", profile, mealType: "Breakfast", fileName: "kanda-poha.jpg");

        Assert.NotNull(result);
        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, item => item.Name.Contains("Poha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FoodVisionService_MealTypeAware_IdentifiesSnackItems()
    {
        byte[] bytes = new byte[1024];
        using var stream = new MemoryStream(bytes);
        var profile = new UserProfile { Name = "Evening Snack User", RegionalCuisine = "North Indian" };

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream, "image/jpeg", "North Indian", profile, mealType: "Snack", fileName: "evening-snack.jpg");

        Assert.NotNull(result);
        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, item => item.Name.Contains("Makhana", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Chai", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FoodVisionService_MealTypeAware_IdentifiesDinnerItems()
    {
        byte[] bytes = new byte[1024];
        using var stream = new MemoryStream(bytes);
        var profile = new UserProfile { Name = "Dinner User", RegionalCuisine = "Gujarati" };

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream, "image/jpeg", "Gujarati", profile, mealType: "Dinner", fileName: "dinner-plate.jpg");

        Assert.NotNull(result);
        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, item => item.Name.Contains("Khichdi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FoodVisionService_MealTypeAware_IdentifiesLunchThaliItems()
    {
        byte[] bytes = new byte[1024];
        using var stream = new MemoryStream(bytes);
        var profile = new UserProfile { Name = "Lunch User", RegionalCuisine = "North Indian" };

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream, "image/jpeg", "North Indian", profile, mealType: "Lunch", fileName: "lunch-thali.jpg");

        Assert.NotNull(result);
        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, item => item.Name.Contains("Roti", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Phulka", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.IdentifiedItems, item => item.Name.Contains("Dal", StringComparison.OrdinalIgnoreCase));
    }
}

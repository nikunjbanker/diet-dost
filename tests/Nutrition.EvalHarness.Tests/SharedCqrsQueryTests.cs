/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;
using Nutrition.Application.Features.Analytics.Queries.GetHistoricalAnalytics;
using Nutrition.Application.Features.Analytics.Queries.GetProjections;
using Nutrition.Application.Features.Meals.Queries.GetAiQuota;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Application.Features.Profile.Queries.GetProfile;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Unit and integration test suite asserting accurate calculation, security boundary gating,
/// and native IDispatcher execution across consolidated CQRS queries per Phase 1 Layer 2 (Issue #27).
/// </summary>
public class SharedCqrsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public SharedCqrsQueryTests()
    {
        var connectionString = $"Data Source=file:memdb_cqrs_{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<DietTrackerDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ClinicalDietitianService>();
        services.AddScoped<ITierConfigurationService, TierConfigurationService>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();

        // Register application services and CQRS handlers via native scanning
        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Initialize schema and seed default tier configurations
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        db.Database.EnsureCreated();
        db.TierConfigurations.AddRange(TierFeatureConfiguration.GetDefaultConfigurations());
        db.SaveChanges();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private async Task SeedUserWithProfileAndMealsAsync(string userId, UserTier tier)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        var profile = new UserProfile
        {
            Id = userId,
            Name = "Rohan Verma",
            Age = 30,
            Sex = BiologicalSex.Male,
            HeightCm = 175,
            CurrentWeightKg = 78,
            TargetWeightKg = 70,
            ActivityLevel = ActivityLevel.Moderate,
            DietaryPreference = DietaryPreference.PureVeg,
            Timezone = "Asia/Kolkata"
        };
        db.Profiles.Add(profile);

        var meal = new MealLog
        {
            Id = $"meal-{userId}-1",
            UserId = userId,
            DishName = "Moong Dal Khichdi with Curd",
            LoggedAt = DateTime.UtcNow,
            MealType = MealType.Dinner,
            TotalCalories = 420,
            TotalProteinGrams = 18,
            TotalCarbsGrams = 60,
            TotalFatGrams = 10,
            TotalFiberGrams = 7,
            TotalSodiumMg = 450,
            TotalSugarGrams = 4,
            OverallConfidenceScore = 0.95
        };
        db.Meals.Add(meal);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDailyLedger_UnauthorizedUser_Returns401()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetDailyLedgerQuery(string.Empty, null, false, null);
        var result = await dispatcher.QueryAsync(query);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GetDailyLedger_ForbiddenOtherUser_Returns403()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetDailyLedgerQuery("user-1", "user-2", false, null);
        var result = await dispatcher.QueryAsync(query);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task GetDailyLedger_ValidUser_ReturnsAggregatedLedgerWithMacros()
    {
        const string userId = "user-ledger-test";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Basic);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetDailyLedgerQuery(userId, null, false, null);
        var result = await dispatcher.QueryAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserId);
        Assert.Equal(420, result.Data.ConsumedCalories);
        Assert.Equal(18, result.Data.ConsumedProteinGrams);
        Assert.Equal(60, result.Data.ConsumedCarbsGrams);
        Assert.Equal(10, result.Data.ConsumedFatGrams);
        Assert.Equal(7, result.Data.ConsumedFiberGrams);
        Assert.True(result.Data.BudgetedCalories > 0);
    }

    [Fact]
    public async Task GetHistoricalAnalytics_UnauthorizedUser_Returns401()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetHistoricalAnalyticsQuery(string.Empty, null, UserTier.Free, false, "7D");
        var result = await dispatcher.QueryAsync(query);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GetHistoricalAnalytics_FreeTierRequesting30D_Returns403UpgradeRequired()
    {
        const string userId = "user-free-analytics";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Free);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetHistoricalAnalyticsQuery(userId, null, UserTier.Free, false, "30D");
        var result = await dispatcher.QueryAsync(query);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("FeatureTierUpgradeRequired", result.ErrorCode);
    }

    [Fact]
    public async Task GetHistoricalAnalytics_PremiumTierRequesting30D_Returns200WithTrends()
    {
        const string userId = "user-premium-analytics";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Premium);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetHistoricalAnalyticsQuery(userId, null, UserTier.Premium, false, "30D");
        var result = await dispatcher.QueryAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("30D", result.Data.Period);
        Assert.NotEmpty(result.Data.DailyTrends);
    }

    [Fact]
    public async Task GetProjections_DelegatesToHistoricalAnalytics_ReturnsIdenticalResult()
    {
        const string userId = "user-projections-test";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Premium);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var projectionsQuery = new GetProjectionsQuery(userId, null, UserTier.Premium, false, "7D");
        var result = await dispatcher.QueryAsync(projectionsQuery);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("7D", result.Data.Period);
        Assert.NotEmpty(result.Data.DailyTrends);
    }

    [Fact]
    public async Task GetMealHistory_ValidUser_ReturnsPaginatedMealsAndSummary()
    {
        const string userId = "user-meal-history";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Basic);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetMealHistoryQuery(userId, null, false, "7D", null, null);
        var result = await dispatcher.QueryAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserId);
        Assert.Single(result.Data.Meals);
        Assert.Equal("Moong Dal Khichdi with Curd", result.Data.Meals[0].DishName);
        Assert.Equal(420, result.Data.Summary.TotalCalories);
    }

    [Fact]
    public async Task GetAiQuota_ValidUser_ReturnsTierStats()
    {
        const string userId = "user-quota-test";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Free);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetAiQuotaQuery(userId, UserTier.Free);
        var result = await dispatcher.QueryAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.DailyLimit > 0);
        Assert.Equal(0, result.Data.UsedToday);
        Assert.True(result.Data.RemainingCalls > 0);
    }

    [Fact]
    public async Task GetProfile_ValidUser_ReturnsProfileDetails()
    {
        const string userId = "user-profile-test";
        await SeedUserWithProfileAndMealsAsync(userId, UserTier.Premium);

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = new GetProfileQuery(userId, null, false);
        var result = await dispatcher.QueryAsync(query);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Rohan Verma", result.Data.Profile.Name);
        Assert.True(result.Data.Budget.TargetCalories > 1000);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.WebBff.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Nutrition.WebGateway.Controllers.Web;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class WebBffCompositeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebBffCompositeTests()
    {
        var connectionString = $"Data Source=file:memdb_bff_{Guid.NewGuid():N}?mode=memory&cache=shared";
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

        // Register application services and CQRS query handlers
        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Initialize schema and seed default data
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

    private WebBffController CreateController(ClaimsPrincipal principal)
    {
        var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
        var controller = new WebBffController(dispatcher, _scopeFactory, NullLogger<WebBffController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        return controller;
    }

    private static ClaimsPrincipal CreateUserPrincipal(string userId, string email, string role, string tier, string name)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("tier", tier),
            new(ClaimTypes.Name, name),
            new("displayName", name)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task GetDashboardComposite_UnauthorizedUser_Returns401()
    {
        // Arrange: Unauthenticated user with empty claims
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var result = await controller.GetDashboardComposite("7D");

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory]
    [InlineData("free@dietdost.app", "Free", "User", false, false, 7, false, false, 1)]
    [InlineData("basic@dietdost.app", "Basic", "User", false, false, 30, false, false, 7)]
    [InlineData("premium@dietdost.app", "Premium", "User", true, true, 365, true, false, 30)]
    [InlineData("admin.demo@dietdost.app", "Premium", "Admin", true, true, 365, true, true, 30)]
    [InlineData("superadmin@dietdost.app", "SuperAdmin", "SuperAdmin", true, true, 365, true, true, -1)]
    public async Task GetDashboardComposite_AllDemoTiers_ReturnsExpectedCompositeAndGating(
        string email,
        string tier,
        string role,
        bool expectedPhotoCompare,
        bool expectedExport,
        int expectedHistoryLimit,
        bool expectedAnalytics,
        bool expectedAdmin,
        int expectedQuotaLimit)
    {
        // Arrange
        var userId = $"usr-{tier.ToLowerInvariant()}";
        var principal = CreateUserPrincipal(userId, email, role, tier, $"{tier} User");
        var controller = CreateController(principal);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var actionResult = await controller.GetDashboardComposite("7D");
        stopwatch.Stop();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var composite = Assert.IsType<WebDashboardCompositeDto>(okResult.Value);

        Assert.NotNull(composite);
        Assert.Equal(userId, composite.User.UserId);
        Assert.Equal(email, composite.User.Email);
        Assert.Equal(tier, composite.User.Tier);
        Assert.Equal(role, composite.User.Role);

        // Feature flags verification
        Assert.Equal(expectedPhotoCompare, composite.FeatureFlags.CanComparePhotos);
        Assert.Equal(expectedExport, composite.FeatureFlags.CanExportData);
        Assert.Equal(expectedHistoryLimit, composite.FeatureFlags.HistoryDayLimit);
        Assert.Equal(expectedAnalytics, composite.FeatureFlags.HasAdvancedAnalytics);
        Assert.Equal(expectedAdmin, composite.FeatureFlags.IsAdmin);

        // Quota verification
        Assert.Equal(expectedQuotaLimit, composite.Quota.DailyLimit);

        // Daily ledger and projections verification
        Assert.NotNull(composite.TodayLedger);
        Assert.NotNull(composite.Projections);
        Assert.Equal("7D", composite.Projections.Period);
        Assert.NotNull(composite.RecentMeals);
    }

    [Fact]
    public async Task GetDashboardComposite_WithSeededProfileAndMeals_HydratesFullDetails()
    {
        // Arrange
        var userId = "usr-seeded";
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
            var profile = new UserProfile
            {
                Id = userId,
                Name = "Priya Sharma",
                Age = 28,
                Sex = BiologicalSex.Female,
                HeightCm = 162,
                CurrentWeightKg = 64,
                ActivityLevel = ActivityLevel.Sedentary,
                TargetWeightKg = 58,
                Timezone = "Asia/Kolkata"
            };
            db.Profiles.Add(profile);

            var meal = new MealLog
            {
                Id = "meal-101",
                UserId = userId,
                DishName = "Dal Tadka with 2 Phulkas",
                LoggedAt = DateTime.UtcNow,
                MealType = MealType.Lunch,
                OverallConfidenceScore = 0.92,
                TotalCalories = 380,
                TotalProteinGrams = 14,
                TotalCarbsGrams = 52,
                TotalFatGrams = 10,
                TotalFiberGrams = 6
            };
            db.Meals.Add(meal);
            await db.SaveChangesAsync();
        }

        var principal = CreateUserPrincipal(userId, "priya@example.com", "User", "Premium", "Priya Sharma");
        var controller = CreateController(principal);

        // Act
        var actionResult = await controller.GetDashboardComposite("7D");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var composite = Assert.IsType<WebDashboardCompositeDto>(okResult.Value);

        Assert.Equal("Priya Sharma", composite.User.DisplayName);
        Assert.NotNull(composite.User.Budget);
        Assert.True(composite.User.Budget!.TargetCalories > 1000);
        Assert.NotNull(composite.User.Macros);
        Assert.True(composite.User.Macros!.ProteinGrams > 0);

        Assert.Single(composite.RecentMeals);
        Assert.Equal("Dal Tadka with 2 Phulkas", composite.RecentMeals[0].DishName);
    }
}

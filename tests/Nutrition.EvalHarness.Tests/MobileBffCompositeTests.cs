/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
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
using Nutrition.Application.Features.Meals.Commands.ConfirmMeal;
using Nutrition.Application.Features.MobileBff.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Nutrition.WebGateway.Controllers.Mobile;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class MobileBffCompositeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public MobileBffCompositeTests()
    {
        var connectionString = $"Data Source=file:memdb_mobile_bff_{Guid.NewGuid():N}?mode=memory&cache=shared;Default Timeout=30;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        using (var pragmaCmd = _connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            pragmaCmd.ExecuteNonQuery();
        }

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<DietTrackerDbContext>(options =>
            options.UseSqlite(connectionString)
                   .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ClinicalDietitianService>();
        services.AddScoped<ITierConfigurationService, TierConfigurationService>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();

        // Register application services and CQRS handlers
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

    private MobileBffController CreateController(ClaimsPrincipal principal, DefaultHttpContext? httpContext = null)
    {
        var context = httpContext ?? new DefaultHttpContext { User = principal };
        var controller = new MobileBffController(_scopeFactory, NullLogger<MobileBffController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
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
            new("displayName", name)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task GetDashboardComposite_Unauthenticated_ReturnsUnauthorized()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateController(principal);

        var result = await controller.GetDashboardComposite();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetDashboardComposite_AuthenticatedUser_Returns200WithEtagAndCompactData()
    {
        var userId = "user-mobile-free-01";
        var principal = CreateUserPrincipal(userId, "free@dietdost.app", "User", "Free", "Free Mobile Tester");

        // Seed profile and a meal
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
            db.Profiles.Add(new UserProfile
            {
                Id = userId,
                Name = "Free Mobile Tester",
                Age = 28,
                Sex = BiologicalSex.Male,
                HeightCm = 175,
                CurrentWeightKg = 72,
                ActivityLevel = ActivityLevel.Sedentary,
                TargetWeightKg = 68,
                Timezone = "Asia/Kolkata"
            });

            db.Meals.Add(new MealLog
            {
                UserId = userId,
                DishName = "Oats Upma",
                MealType = MealType.Breakfast,
                LoggedAt = DateTime.UtcNow,
                TotalCalories = 320,
                TotalProteinGrams = 12.5,
                TotalCarbsGrams = 48.0,
                TotalFatGrams = 7.0
            });
            await db.SaveChangesAsync();
        }

        var httpContext = new DefaultHttpContext { User = principal };
        var controller = CreateController(principal, httpContext);

        var actionResult = await controller.GetDashboardComposite();

        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var composite = Assert.IsType<MobileDashboardCompositeDto>(jsonResult.Value);

        // Assert summary
        Assert.NotNull(composite.Summary);
        Assert.Equal(320.0, composite.Summary.ConsumedCalories);
        Assert.True(composite.Summary.BudgetCalories > 0);
        Assert.Equal(12.5, composite.Summary.Macros.ProteinGrams);

        // Assert today meals
        Assert.Single(composite.TodayMeals);
        Assert.Equal("Oats Upma", composite.TodayMeals[0].Name);
        Assert.Equal(320.0, composite.TodayMeals[0].Calories);

        // Assert quota
        Assert.NotNull(composite.Quota);
        Assert.Equal("Free", composite.Quota.Tier);
        Assert.Equal(1, composite.Quota.DailyLimit);

        // Assert ETag headers
        Assert.True(httpContext.Response.Headers.ContainsKey("ETag"));
        var etagHeader = httpContext.Response.Headers.ETag.ToString();
        Assert.StartsWith("W/\"", etagHeader);
        Assert.Equal(etagHeader, composite.Etag);
        Assert.Equal("private, no-cache", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetDashboardComposite_IfNoneMatch_MatchesEtag_Returns304NotModified()
    {
        var userId = "user-etag-tester";
        var principal = CreateUserPrincipal(userId, "etag@dietdost.app", "User", "Basic", "ETag Tester");

        var firstContext = new DefaultHttpContext { User = principal };
        var firstController = CreateController(principal, firstContext);

        var firstResult = await firstController.GetDashboardComposite();
        var jsonResult = Assert.IsType<JsonResult>(firstResult);
        var composite = Assert.IsType<MobileDashboardCompositeDto>(jsonResult.Value);
        var originalEtag = composite.Etag;
        Assert.NotNull(originalEtag);

        // Second request sends If-None-Match matching original ETag
        var secondContext = new DefaultHttpContext { User = principal };
        secondContext.Request.Headers["If-None-Match"] = originalEtag;
        var secondController = CreateController(principal, secondContext);

        var secondResult = await secondController.GetDashboardComposite();

        var statusResult = Assert.IsType<StatusCodeResult>(secondResult);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
        Assert.Equal(originalEtag, secondContext.Response.Headers.ETag.ToString());
        Assert.Equal("private, no-cache", secondContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetDashboardComposite_CompactJson_OmitsNullsAndUsesCamelCase()
    {
        var userId = "user-compact-json";
        var principal = CreateUserPrincipal(userId, "compact@dietdost.app", "User", "Premium", "Compact JSON Tester");

        var httpContext = new DefaultHttpContext { User = principal };
        var controller = CreateController(principal, httpContext);

        var actionResult = await controller.GetDashboardComposite();
        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var composite = Assert.IsType<MobileDashboardCompositeDto>(jsonResult.Value);

        // Serialize using MobileJsonOptions
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(composite, MobileBffController.MobileJsonOptions);
        var jsonDoc = JsonDocument.Parse(jsonBytes);
        var root = jsonDoc.RootElement;

        // Verify camelCase root properties
        Assert.True(root.TryGetProperty("summary", out var summaryElem));
        Assert.True(root.TryGetProperty("todayMeals", out _));
        Assert.True(root.TryGetProperty("quota", out _));
        Assert.True(root.TryGetProperty("etag", out _));

        // Verify summary camelCase
        Assert.True(summaryElem.TryGetProperty("budgetCalories", out _));
        Assert.True(summaryElem.TryGetProperty("consumedCalories", out _));
        Assert.True(summaryElem.TryGetProperty("remainingCalories", out _));

        // Verify null omission: when no meals have photos, imageUrl is omitted
        if (root.TryGetProperty("todayMeals", out var mealsElem) && mealsElem.GetArrayLength() > 0)
        {
            var firstMeal = mealsElem[0];
            Assert.False(firstMeal.TryGetProperty("imageUrl", out _));
        }

        // Verify payload size is ultra compact (< 4 KB uncompressed for clean slate)
        Assert.True(jsonBytes.Length < 4096, $"Payload size {jsonBytes.Length} bytes exceeds 4 KB threshold");
    }

    [Theory]
    [InlineData("Free", 1, 7, false)]
    [InlineData("Basic", 7, 30, false)]
    [InlineData("Premium", 30, 365, true)]
    public async Task GetDashboardComposite_AcrossUserTiers_EnforcesTierLimits(
        string tier, int expectedDailyLimit, int expectedHistoryLimit, bool expectedAdvancedAnalytics)
    {
        var userId = $"user-tier-{tier.ToLowerInvariant()}";
        var principal = CreateUserPrincipal(userId, $"{tier.ToLowerInvariant()}@dietdost.app", "User", tier, $"{tier} User");

        var httpContext = new DefaultHttpContext { User = principal };
        var controller = CreateController(principal, httpContext);

        var actionResult = await controller.GetDashboardComposite();
        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var composite = Assert.IsType<MobileDashboardCompositeDto>(jsonResult.Value);

        Assert.Equal(tier, composite.Quota.Tier);
        Assert.Equal(expectedDailyLimit, composite.Quota.DailyLimit);
        Assert.NotNull(composite.FeatureFlags);
        Assert.Equal(expectedHistoryLimit, composite.FeatureFlags.HistoryDayLimit);
        Assert.Equal(expectedAdvancedAnalytics, composite.FeatureFlags.HasAdvancedAnalytics);
    }

    [Fact]
    public async Task ConfirmMealCommand_WithClientMutationId_IsIdempotentAcrossRetries()
    {
        var userId = "user-offline-sync-01";
        var clientMutationId = Guid.NewGuid().ToString("N");
        var clientTimestamp = DateTime.UtcNow;

        var meal1 = new MealLog
        {
            UserId = userId,
            DishName = "Paneer Tikka with Mint Chutney",
            MealType = MealType.Dinner,
            LoggedAt = clientTimestamp,
            ClientMutationId = clientMutationId,
            ClientTimestampUtc = clientTimestamp,
            Items = new List<FoodItemRecord>
            {
                new()
                {
                    Name = "Paneer Tikka",
                    Calories = 350,
                    ProteinGrams = 22,
                    CarbsGrams = 8,
                    FatGrams = 25
                }
            }
        };

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // First transmission attempt
        var command1 = new ConfirmMealCommand(meal1, userId, false, clientMutationId, clientTimestamp);
        var result1 = await dispatcher.SendAsync(command1);
        Assert.True(result1.Succeeded);
        Assert.NotNull(result1.Data);
        var savedMealId1 = result1.Data.Meal.Id;

        // Second retry attempt (simulating 4G packet loss retry with identical clientMutationId)
        var meal2 = new MealLog
        {
            UserId = userId,
            DishName = "Paneer Tikka with Mint Chutney",
            MealType = MealType.Dinner,
            LoggedAt = clientTimestamp,
            ClientMutationId = clientMutationId,
            ClientTimestampUtc = clientTimestamp,
            Items = new List<FoodItemRecord>
            {
                new()
                {
                    Name = "Paneer Tikka",
                    Calories = 350,
                    ProteinGrams = 22,
                    CarbsGrams = 8,
                    FatGrams = 25
                }
            }
        };

        var command2 = new ConfirmMealCommand(meal2, userId, false, clientMutationId, clientTimestamp);
        var result2 = await dispatcher.SendAsync(command2);
        Assert.True(result2.Succeeded);
        Assert.NotNull(result2.Data);

        // Assert that the exact same meal record was returned without creating a duplicate in database
        Assert.Equal(savedMealId1, result2.Data.Meal.Id);

        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        var totalMealsCount = await db.Meals.CountAsync(m => m.UserId == userId && m.ClientMutationId == clientMutationId);
        Assert.Equal(1, totalMealsCount);
    }
}

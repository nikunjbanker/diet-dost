/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Moq;
using Nutrition.Application.Agents;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Analytics.DTOs;
using Nutrition.Application.Features.Meals.Commands.AnalyzeTextMeal;
using Nutrition.Application.Features.Meals.Commands.ConfirmMeal;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Features.Meals.Queries.EstimateFoodItem;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Mcp.Models;
using Nutrition.Mcp.Security;
using Nutrition.Mcp.Tools;
using Xunit;

namespace Nutrition.Mcp.Tests;

public class MealToolsTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IMcpContinuationTokenProtector> _tokenProtectorMock;
    private readonly MealTools _mealTools;
    private readonly ClaimsPrincipal _user;

    public MealToolsTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _tokenProtectorMock = new Mock<IMcpContinuationTokenProtector>();
        _mealTools = new MealTools(_tokenProtectorMock.Object);

        _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test@dietdost.app"),
            new Claim("tier", "Premium")
        }, "TestAuth"));
    }

    [Fact]
    public async Task ViewMealLogs_DispatchesQuery_AndReturnsFormattedTotals()
    {
        var summary = new MealHistorySummaryDto(
            TotalCalories: 500,
            TotalProteinGrams: 25,
            TotalCarbsGrams: 60,
            TotalFatGrams: 15,
            TotalFiberGrams: 8,
            TotalSugarGrams: 5,
            TotalSodiumMg: 600
        );

        var responseDto = new MealHistoryResponseDto(
            UserId: "test@dietdost.app",
            Period: "day",
            SelectedDate: "2026-11-18",
            MealType: null,
            TotalMealsCount: 1,
            Summary: summary,
            Meals: new List<MealLogDto>
            {
                new MealLog
                {
                    Id = "meal-1",
                    UserId = "test@dietdost.app",
                    MealType = MealType.Breakfast,
                    DishName = "Oatmeal & Almonds",
                    IsVerifiedByUser = true,
                    TotalCalories = 500,
                    Items = new List<FoodItemRecord>
                    {
                        new() { Name = "Oatmeal", EstimatedPortion = "1 bowl", Calories = 350, ProteinGrams = 15, CarbsGrams = 50, FatGrams = 5, FiberGrams = 6 }
                    }
                }.ToDto()
            }
        );

        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetMealHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MealHistoryResponseDto>.Success(responseDto));

        var result = await _mealTools.ViewMealLogs(_dispatcherMock.Object, period: "day", date: "2026-11-18", user: _user);
        Assert.NotNull(result);

        var typed = Assert.IsType<ViewMealLogsResult>(result);
        Assert.Equal("test@dietdost.app", typed.User);
        Assert.Equal(1, typed.TotalMealsCount);
        Assert.Equal(500.0, typed.NutritionTotals.Calories);
    }

    [Fact]
    public async Task EstimateFoodPortion_DispatchesEstimateQuery_AndReturnsItem()
    {
        var estimate = new FoodItemNutritionEstimate(
            NormalizedName: "Paneer Bhurji",
            HindiOrRegionalName: "पनीर भुर्जी",
            EstimatedPortion: "1 bowl (150g)",
            Grams: 150,
            Calories: 280,
            ProteinGrams: 18,
            CarbsGrams: 6,
            FatGrams: 20,
            FiberGrams: 2,
            SodiumMg: 450,
            CookingMediumEstimate: "Home Cooking",
            Source: "IFCT 2017",
            ConfidenceScore: 0.95,
            SugarGrams: 1
        );

        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<EstimateFoodItemQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FoodItemNutritionEstimate>.Success(estimate));

        var result = await _mealTools.EstimateFoodPortion(_dispatcherMock.Object, foodName: "Paneer Bhurji", portion: "1 bowl", user: _user);
        Assert.NotNull(result);

        var typed = Assert.IsType<FoodPortionEstimateResult>(result);
        Assert.Equal("Paneer Bhurji", typed.Dish);
        Assert.Equal(280.0, typed.Calories);
        Assert.Equal(18.0, typed.ProteinGrams);
    }

    [Fact]
    public async Task AddMealEntry_InitialTextCall_ReturnsElicitationRequired_WithToken()
    {
        var analysis = new IndianMealAnalysisResult
        {
            MealType = "Lunch",
            DishName = "2 Rotis & Dal",
            TotalCalories = 420,
            TotalProteinGrams = 16,
            TotalCarbsGrams = 60,
            TotalFatGrams = 12,
            TotalFiberGrams = 6,
            IdentifiedItems = new List<IndianMealItemDto>
            {
                new() { Name = "Roti", EstimatedPortion = "2 pieces", Calories = 240, ProteinGrams = 6, CarbsGrams = 46, FatGrams = 4, FiberGrams = 4 },
                new() { Name = "Dal Tadka", EstimatedPortion = "1 bowl", Calories = 180, ProteinGrams = 10, CarbsGrams = 14, FatGrams = 8, FiberGrams = 2 }
            }
        };

        var resultDto = new MealAnalysisResultDto(
            ConfidenceGated: false,
            ConfidenceScore: 0.9,
            DetectedByModel: "Gemini",
            PhotoUrl: null,
            Message: "Analysis complete",
            Advice: null,
            RequiresRetake: false,
            Analysis: analysis
        );

        _dispatcherMock.Setup(d => d.SendAsync(It.IsAny<AnalyzeTextMealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MealAnalysisResultDto>.Success(resultDto));

        _tokenProtectorMock.Setup(p => p.Protect(It.IsAny<DraftMealContinuationState>()))
            .Returns("encrypted-approval-token-xyz");

        var result = await _mealTools.AddMealEntry(
            dispatcher: _dispatcherMock.Object,
            user: _user,
            textDescription: "2 rotis and dal for lunch"
        );

        Assert.NotNull(result);
        var typed = Assert.IsType<MealElicitationRequiredResult>(result);
        Assert.Equal("ElicitationRequired", typed.Status);
        Assert.Equal("encrypted-approval-token-xyz", typed.ApprovalToken);
        Assert.Equal(420.0, typed.ProposedMeal.TotalCalories);

        // Assert that zero meals were saved to DB on stage 1
        _dispatcherMock.Verify(d => d.SendAsync(It.IsAny<ConfirmMealCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMealEntry_ApprovedContinuation_CommitsMeal_WithIsVerifiedByUserTrue()
    {
        var pendingState = new DraftMealContinuationState(
            UserId: "test@dietdost.app",
            MealType: "Lunch",
            Description: "2 Rotis & Dal",
            TotalCalories: 420,
            TotalProtein: 16,
            TotalCarbs: 60,
            TotalFat: 12,
            TotalFiber: 6,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            Nonce: Guid.NewGuid().ToString("N"),
            Items: new List<DraftItem>
            {
                new("Roti", "2 pieces", 240, 6, 46, 4, 4),
                new("Dal Tadka", "1 bowl", 180, 10, 14, 8, 2)
            }
        );

        _tokenProtectorMock.Setup(p => p.Unprotect("valid-token", "test@dietdost.app"))
            .Returns(pendingState);

        var confirmDto = new ConfirmMealResultDto(
            Meal: new MealLog { Id = "meal-verified-99", UserId = "test@dietdost.app", IsVerifiedByUser = true, TotalCalories = 420 }.ToDto(),
            DailyLedger: new DailyCalorieLedger().ToDto(),
            LearnedNotes: new List<string>(),
            LearnedMessage: null,
            Message: "Logged"
        );

        _dispatcherMock.Setup(d => d.SendAsync(It.IsAny<ConfirmMealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfirmMealResultDto>.Success(confirmDto));

        var result = await _mealTools.AddMealEntry(
            dispatcher: _dispatcherMock.Object,
            user: _user,
            approvalToken: "valid-token",
            approved: "yes"
        );

        Assert.NotNull(result);
        var typed = Assert.IsType<MealConfirmedResult>(result);
        Assert.Equal("Confirmed", typed.Status);
        Assert.Equal("meal-verified-99", typed.MealId);

        // Verify ConfirmMealCommand was called with expected DishName and CurrentUserId
        _dispatcherMock.Verify(d => d.SendAsync(It.Is<ConfirmMealCommand>(c => c.Meal.DishName == "2 Rotis & Dal" && c.CurrentUserId == "test@dietdost.app"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMealEntry_RejectedContinuation_CancelsOperation_WithZeroWrites()
    {
        var result = await _mealTools.AddMealEntry(
            dispatcher: _dispatcherMock.Object,
            user: _user,
            approvalToken: "some-token",
            approved: "no"
        );

        Assert.NotNull(result);
        var typed = Assert.IsType<MealCancelledResult>(result);
        Assert.Equal("Cancelled", typed.Status);
        _dispatcherMock.Verify(d => d.SendAsync(It.IsAny<ConfirmMealCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMealEntry_DedicatedTool_CommitsMeal_WhenApproved()
    {
        var pendingState = new DraftMealContinuationState(
            UserId: "test@dietdost.app",
            MealType: "Snack",
            Description: "Masala Chai",
            TotalCalories: 110,
            TotalProtein: 3,
            TotalCarbs: 15,
            TotalFat: 4,
            TotalFiber: 0,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            Nonce: Guid.NewGuid().ToString("N"),
            Items: new List<DraftItem>
            {
                new("Masala Chai", "1 cup", 110, 3, 15, 4, 0)
            }
        );

        _tokenProtectorMock.Setup(p => p.Unprotect("tea-token", "test@dietdost.app"))
            .Returns(pendingState);

        var confirmDto = new ConfirmMealResultDto(
            Meal: new MealLog { Id = "snack-101", UserId = "test@dietdost.app", IsVerifiedByUser = true, TotalCalories = 110 }.ToDto(),
            DailyLedger: new DailyCalorieLedger().ToDto(),
            LearnedNotes: new List<string>(),
            LearnedMessage: null,
            Message: "Logged"
        );

        _dispatcherMock.Setup(d => d.SendAsync(It.IsAny<ConfirmMealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfirmMealResultDto>.Success(confirmDto));

        var result = await _mealTools.ConfirmMealEntry(
            dispatcher: _dispatcherMock.Object,
            user: _user,
            approvalToken: "tea-token",
            approved: "yes"
        );

        Assert.NotNull(result);
        var typed = Assert.IsType<MealConfirmedResult>(result);
        Assert.Equal("Confirmed", typed.Status);
        Assert.Equal("snack-101", typed.MealId);
    }
}

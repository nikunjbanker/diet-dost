/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.AI;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class LocalClinicalEngineFallbackTests
{
    [Theory]
    [InlineData("2 rotis and dal")]
    [InlineData("kanda poha with chai")]
    [InlineData("idli sambar")]
    [InlineData("homestyle khichdi")]
    public async Task AnalyzeMealDescriptionAsync_LocalClinicalEngine_ProvidesConfidenceThresholdAndFullMacroBreakdown(string description)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "AI:ModelId", "offline-local-engine" },
            { "AI:Endpoint", "http://localhost:11434" } // Unreachable endpoint forces local offline fallback
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var logger = NullLogger<MicrosoftAgentFoodVisionService>.Instance;
        var service = new MicrosoftAgentFoodVisionService(config, logger, new HttpClient());

        var profile = new UserProfile
        {
            Name = "Test User",
            Age = 30,
            HeightCm = 170,
            CurrentWeightKg = 70
        };

        var result = await service.AnalyzeMealDescriptionAsync(description, "Lunch", profile, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsConfidenceGatedPassed, $"Offline result confidence ({result.OverallConfidenceScore}) must be >= 0.70 threshold to allow saving without forced retake");
        Assert.True(result.OverallConfidenceScore >= 0.70, "Overall confidence score must be >= 0.70");
        Assert.NotEmpty(result.IdentifiedItems);
        Assert.True(result.TotalCalories > 0, "Total calories must be calculated");
        Assert.True(result.TotalProteinGrams >= 0, "Protein must be calculated");
        Assert.True(result.TotalCarbsGrams >= 0, "Carbs must be calculated");
        Assert.True(result.TotalFatGrams >= 0, "Fat must be calculated");
        Assert.False(string.IsNullOrWhiteSpace(result.DishName), "Dish name must not be blank");
    }
}

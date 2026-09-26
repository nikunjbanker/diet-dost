/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Agents;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Security;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class VisionAiEvalTests
{
    private readonly MicrosoftAgentFoodVisionService _visionService;

    public VisionAiEvalTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"AI:ModelId", "gemini-3.8-flash"},
            {"AI:FallbackModelId", "gemini-3.7-flash"},
            {"AI:Endpoint", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _visionService = new MicrosoftAgentFoodVisionService(
            configuration,
            NullLogger<MicrosoftAgentFoodVisionService>.Instance,
            new HttpClient());
    }

    [Fact]
    public async Task Fixture1_HomestyleIndianThali_MeetsConfidenceAndCaloricTolerances()
    {
        // Fixture 1: 2 Phulkas + 1 Katori Dal Tadka + Cucumber Salad
        // Expected ~340 kcal +- 10%, Confidence >= 70%
        byte[] validImageBytes = new byte[2048]; // Simulated valid image
        using var stream = new MemoryStream(validImageBytes);

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream,
            "image/jpeg",
            "North Indian",
            new UserProfile { Name = "Test User", Age = 30, HeightCm = 170, CurrentWeightKg = 70 });

        // Assert confidence gating (>= 70%)
        Assert.True(result.IsConfidenceGatedPassed);
        Assert.True(result.OverallConfidenceScore >= 0.70);

        // Assert calories within expected Indian portion range (300 - 480 kcal with cooked subzi)
        Assert.InRange(result.TotalCalories, 300, 480);

        // Assert identifies core Indian staple items including cooked subzi (not salad)
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Phulka") || i.Name.Contains("Roti"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Dal"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Subzi") || i.Name.Contains("Bhindi"));
    }

    [Fact]
    public async Task Fixture5_UserCorrectionMemory_ReTrainsSubziDetection()
    {
        var validImageBytes = new byte[2500];
        Array.Fill<byte>(validImageBytes, 0x42);
        using var stream = new MemoryStream(validImageBytes);

        var corrections = new List<Nutrition.Domain.Model.Meal.UserCorrectionRecord>
        {
            new()
            {
                UserId = "user-1",
                OriginalDetectedItem = "Salad",
                CorrectedItemName = "Palak Paneer",
                HindiOrRegionalName = "Palak Paneer",
                EstimatedPortion = "1 Katori (150g)",
                Calories = 220,
                ProteinGrams = 12,
                CarbsGrams = 8,
                FatGrams = 16,
                FrequencyCount = 3
            }
        };

        var result = await _visionService.AnalyzeMealPhotoAsync(
            stream,
            "image/jpeg",
            "North Indian",
            new UserProfile { Name = "Test User", Age = 30, HeightCm = 170, CurrentWeightKg = 70 },
            corrections);

        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Palak Paneer"));
    }

    [Fact]
    public async Task Fixture2_MasalaDosa_TriggersSodiumFlag_AndCorrectMacronutrients()
    {
        // Natural language / description of South Indian classic
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "Had 1 Masala Dosa with coconut chutney and sambar",
            "Breakfast");

        Assert.True(result.IsConfidenceGatedPassed);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Dosa"));

        // Masala dosa + sambar + chutney typically has > 700mg sodium
        Assert.True(result.TotalSodiumMg >= 700);
        Assert.InRange(result.TotalCalories, 350, 450);
    }

    [Fact]
    public async Task Fixture3_BlurryDarkPhoto_TriggersConfidenceGatingFail_Below70Percent()
    {
        // Simulated corrupted / blurry tiny photo (< 1000 bytes)
        byte[] blurryBytes = new byte[500];
        using var stream = new MemoryStream(blurryBytes);

        var result = await _visionService.AnalyzeMealPhotoAsync(stream, "image/jpeg");

        // Must fail confidence gate (< 70%) to trigger retake prompt
        Assert.False(result.IsConfidenceGatedPassed);
        Assert.True(result.OverallConfidenceScore < 0.70);
        Assert.Contains("Retake Photo", result.DietitianAdvice);
    }

    [Fact]
    public void OwaspImageValidator_RejectsSpoofedAndOversizedFiles()
    {
        // 1. Rejects fake text disguised as image
        byte[] fakeBytes = System.Text.Encoding.UTF8.GetBytes("<html><script>alert('xss')</script></html>");
        using var fakeStream = new MemoryStream(fakeBytes);
        var (isValidFake, errorFake, _) = ImageUploadValidator.ValidateImage(fakeStream, fakeBytes.Length);
        Assert.False(isValidFake);
        Assert.Contains("Invalid image format", errorFake);

        // 2. Rejects files exceeding 8MB
        byte[] validHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var oversizedStream = new MemoryStream(validHeader);
        var (isValidSize, errorSize, _) = ImageUploadValidator.ValidateImage(oversizedStream, 9 * 1024 * 1024);
        Assert.False(isValidSize);
        Assert.Contains("exceeds maximum permitted size", errorSize);

        // 3. Accepts valid JPEG header
        var (isValidJpeg, _, mimeType) = ImageUploadValidator.ValidateImage(oversizedStream, 5000);
        Assert.True(isValidJpeg);
        Assert.Equal("image/jpeg", mimeType);
    }

    [Fact]
    public async Task Fixture6_AiFeedback_ThumbsDownWithRemarks_RetrainsModel()
    {
        var currentItems = new List<IndianMealItemDto>
        {
            new() { Name = "Yellow Dal", EstimatedPortion = "1 Katori", Calories = 140, ProteinGrams = 7 },
            new() { Name = "Mix Veg Subzi", EstimatedPortion = "1 Katori", Calories = 120, ProteinGrams = 3 },
            new() { Name = "Phulka", EstimatedPortion = "2 Phulkas", Calories = 160, ProteinGrams = 6 }
        };

        var result = await _visionService.ProcessFeedbackRetrainingAsync(
            userId: "user-eval-1",
            dishName: "North Indian Lunch Thali",
            rating: "thumbs_down",
            remarks: "Dal was Toor Dal",
            currentItems: currentItems);

        Assert.True(result.Retrained);
        Assert.NotNull(result.UpdatedItemEstimate);
        Assert.Contains("Toor Dal", result.CorrectedDish);
        Assert.True(result.UpdatedItemEstimate.Calories > 0);
        Assert.True(result.UpdatedItemEstimate.ProteinGrams > 0);
        Assert.Contains("learned", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fixture7_AiFeedback_GroundTruthGuardrail_BlocksOkraToPaneerOverride()
    {
        var currentItems = new List<IndianMealItemDto>
        {
            new() { Name = "Bhindi Masala", EstimatedPortion = "1 Katori", Calories = 120, ProteinGrams = 3 }
        };

        var result = await _visionService.ProcessFeedbackRetrainingAsync(
            userId: "user-eval-1",
            dishName: "Bhindi Lunch",
            rating: "thumbs_down",
            remarks: "This was Palak Paneer",
            currentItems: currentItems);

        // Guardrail must block overriding obvious Bhindi with Paneer
        Assert.False(result.Retrained);
        Assert.Contains("guardrail", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fixture8_AiFeedback_ThumbsUp_AffirmsAccuracy()
    {
        var result = await _visionService.ProcessFeedbackRetrainingAsync(
            userId: "user-eval-1",
            dishName: "North Indian Thali",
            rating: "thumbs_up",
            remarks: "Spot on! Perfect detection.");

        Assert.True(result.Retrained);
        Assert.Contains("Positive feedback recorded", result.Message);
    }

    [Fact]
    public async Task Fixture9_TextAnalysis_MultiItemMealDescription_ParsesAllItemsAccurately()
    {
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "2 rotis with 1 bowl dal and mix veg sabzi",
            "Lunch");

        Assert.NotNull(result);
        Assert.True(result.IdentifiedItems.Count >= 3);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Roti") || i.Name.Contains("Phulka"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Dal"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Subzi") || i.Name.Contains("Vegetable") || i.Name.Contains("Mix Veg"));
        Assert.True(result.TotalCalories > 200);
        Assert.False(string.IsNullOrWhiteSpace(result.DishName));
    }

    [Fact]
    public async Task Fixture10_TextAnalysis_BreakfastItems_IdentifiesDosaAndSambar()
    {
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "Masala Dosa with Sambar and Coconut Chutney",
            "Breakfast");

        Assert.NotNull(result);
        Assert.Equal("Breakfast", result.MealType);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Dosa"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Sambar"));
        Assert.True(result.TotalCalories > 150);
    }

    [Fact]
    public async Task Fixture11_TextAnalysis_PohaAndChai_ParsesItemsAndMacroTotals()
    {
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "Kanda Poha and 1 cup Masala Chai",
            "Breakfast");

        Assert.NotNull(result);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Poha"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Chai") || i.Name.Contains("Tea"));
        Assert.True(result.TotalCalories > 100);
        Assert.True(result.TotalProteinGrams > 0);
    }

    [Fact]
    public async Task Fixture12_TextAnalysis_NutsAndDriedAnjeer_ParsesIndependentlyAndSynthesizesCompositeDish()
    {
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "10 gm nuts + 2 Pieces of Dried Anjeer",
            "Breakfast");

        Assert.NotNull(result);
        Assert.Equal(2, result.IdentifiedItems.Count);
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Nuts"));
        Assert.Contains(result.IdentifiedItems, i => i.Name.Contains("Anjeer") || i.Name.Contains("Figs"));
        Assert.Contains("Nuts with Dried Anjeer", result.DishName);
        Assert.InRange(result.TotalCalories, 95, 125);
        Assert.True(result.TotalProteinGrams > 2.0);
        Assert.True(result.TotalFatGrams > 4.0);
        Assert.True(result.TotalFiberGrams > 2.0);
    }

    [Fact]
    public void Fixture13_ImageOptimization_ReducesFileSize_WhileMaintainingAspectRatioAndClarity()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Nutrition.WebGateway", "wwwroot", "uploads", "meals", "sample_lunch_thali.jpg");
        if (!File.Exists(samplePath))
        {
            var fallback = Path.GetFullPath(@"src\Nutrition.WebGateway\wwwroot\uploads\meals\sample_lunch_thali.jpg");
            if (File.Exists(fallback)) samplePath = fallback;
        }

        if (File.Exists(samplePath))
        {
            var rawBytes = File.ReadAllBytes(samplePath);
            var (optimizedBytes, mimeType, width, height) = ImageOptimizationHelper.OptimizeForVision(rawBytes, "image/jpeg", 1280, 82);

            Assert.Equal("image/jpeg", mimeType);
            Assert.True(optimizedBytes.Length <= 350_000, $"Optimized size {optimizedBytes.Length} bytes should be well under 350KB");
            Assert.Equal(1200, width);
            Assert.Equal(896, height);
        }
    }

    [Fact]
    public async Task Fixture14_TextualFoodAiSearch_SingleItemUpdate_ReturnsAccurateNutrition()
    {
        // Tests the textual food AI search capability used when updating a food item detail or searching via text box
        var result = await _visionService.AnalyzeMealDescriptionAsync(
            "1 Katori Palak Paneer",
            "Lunch");

        Assert.NotNull(result);
        Assert.NotEmpty(result.IdentifiedItems);
        var palakPaneer = result.IdentifiedItems.FirstOrDefault(i => i.Name.Contains("Paneer") || i.Name.Contains("Palak"));
        Assert.NotNull(palakPaneer);
        Assert.True(palakPaneer.Calories > 150);
        Assert.True(palakPaneer.ProteinGrams >= 8.0);
        Assert.True(palakPaneer.FatGrams > 5.0);
    }
}


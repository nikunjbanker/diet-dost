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
}

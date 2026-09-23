using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Infrastructure.AI;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class AiProviderFactoryTests
{
    [Fact]
    public void FromConfiguration_DefaultsToGoogleAI_WhenNotSpecified()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = AiProviderOptions.FromConfiguration(config);

        Assert.Equal("GoogleAI", options.Provider);
        Assert.Equal("gemini-3-flash-preview", options.ModelId);
        Assert.Equal("gemini-3.6-flash", options.FallbackModelId);
    }

    [Fact]
    public void FromConfiguration_ReadsNestedGoogleAISettings()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "GoogleAI",
            ["AI:GoogleAI:ModelId"] = "gemini-3.8-flash",
            ["AI:GoogleAI:FallbackModelId"] = "gemini-3.7-flash",
            ["AI:GoogleAI:ApiKey"] = "test-gemini-key-12345",
            ["AI:GoogleAI:Endpoint"] = "https://custom-gemini-endpoint.com"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var options = AiProviderOptions.FromConfiguration(config);

        Assert.Equal("GoogleAI", options.Provider);
        Assert.Equal("gemini-3.8-flash", options.ModelId);
        Assert.Equal("gemini-3.7-flash", options.FallbackModelId);
        Assert.Equal("test-gemini-key-12345", options.ApiKey);
        Assert.Equal("https://custom-gemini-endpoint.com", options.Endpoint);
    }

    [Fact]
    public void FromConfiguration_ReadsAzureOpenAISettings()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "AzureOpenAI",
            ["AI:AzureOpenAI:DeploymentName"] = "gpt-5.6-luna",
            ["AI:AzureOpenAI:Endpoint"] = "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1",
            ["AI:AzureOpenAI:ApiKey"] = "test-azure-openai-key-67890"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var options = AiProviderOptions.FromConfiguration(config);

        Assert.Equal("AzureOpenAI", options.Provider);
        Assert.Equal("gpt-5.6-luna", options.AzureDeploymentName);
        Assert.Equal("https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1", options.AzureEndpoint);
        Assert.Equal("test-azure-openai-key-67890", options.AzureApiKey);
        
        var models = options.GetModels();
        Assert.Single(models);
        Assert.Equal("gpt-5.6-luna", models[0]);
    }

    [Fact]
    public void Factory_CreatesGoogleGeminiProvider_WhenGoogleAISelected()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "GoogleAI"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var provider = AiFoodProviderFactory.Create(config, new HttpClient(), NullLoggerFactory.Instance);

        Assert.NotNull(provider);
        Assert.Equal("google_gemini", provider.ProviderName);
        Assert.True(provider.SupportsVision);
    }

    [Fact]
    public void Factory_CreatesAzureOpenAiProvider_WhenAzureOpenAISelected()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "AzureOpenAI",
            ["AI:AzureOpenAI:DeploymentName"] = "gpt-5.6-luna",
            ["AI:AzureOpenAI:Endpoint"] = "https://mf-proj-nikunj-ai-demo--resource.services.ai.azure.com/openai/v1",
            ["AI:AzureOpenAI:ApiKey"] = "test-azure-key-12345678901234567890"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var provider = AiFoodProviderFactory.Create(config, new HttpClient(), NullLoggerFactory.Instance);

        Assert.NotNull(provider);
        Assert.Equal("azure_openai", provider.ProviderName);
        Assert.True(provider.SupportsVision);
    }

    [Fact]
    public void AiJsonParser_ParsesFencedMarkdownJsonCorrectly()
    {
        var rawOutput = """
            Here is the meal analysis:
            ```json
            {
              "dishName": "Dal Tadka with Jeera Rice",
              "mealType": "Lunch",
              "identifiedItems": [
                {
                  "name": "Dal Tadka",
                  "hindiOrRegionalName": "Pili Dal",
                  "estimatedPortion": "1 Katori (150g)",
                  "grams": 150,
                  "calories": 160,
                  "proteinGrams": 7.0,
                  "carbsGrams": 22.0,
                  "fatGrams": 4.5,
                  "fiberGrams": 4.0,
                  "sodiumMg": 280,
                  "cookingMediumEstimate": "Ghee Tadka"
                },
                {
                  "name": "Jeera Rice",
                  "hindiOrRegionalName": "Jeera Rice",
                  "estimatedPortion": "1 Katori (150g)",
                  "grams": 150,
                  "calories": 190,
                  "proteinGrams": 3.5,
                  "carbsGrams": 38.0,
                  "fatGrams": 2.5,
                  "fiberGrams": 1.0,
                  "sodiumMg": 150,
                  "cookingMediumEstimate": "1/2 tsp Cumin Oil"
                }
              ],
              "dietitianAdvice": "Good complex carbohydrates and plant protein balance."
            }
            ```
            Hope this helps!
            """;

        var result = AiJsonParser.Parse(rawOutput, NullLogger.Instance, "gpt-5.6-luna");

        Assert.NotNull(result);
        Assert.Equal("Dal Tadka with Jeera Rice", result.DishName);
        Assert.Equal(2, result.IdentifiedItems.Count);
        Assert.Equal(350, result.TotalCalories);
        Assert.Equal(10.5, result.TotalProteinGrams);
        Assert.Equal(60.0, result.TotalCarbsGrams);
        Assert.Equal(7.0, result.TotalFatGrams);
    }
}

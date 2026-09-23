using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nutrition.Infrastructure.AI;

internal static class AiFoodProviderFactory
{
    public static IAiFoodAnalysisProvider Create(
        IConfiguration configuration,
        HttpClient httpClient,
        ILoggerFactory loggerFactory)
    {
        var options = AiProviderOptions.FromConfiguration(configuration);
        return options.Provider switch
        {
            "AzureOpenAI" => new AzureOpenAiProvider(options, loggerFactory.CreateLogger<AzureOpenAiProvider>()),
            "GoogleAI" => new GoogleGeminiProvider(options, httpClient, loggerFactory.CreateLogger<GoogleGeminiProvider>()),
            _ => throw new InvalidOperationException(
                $"Unsupported AI provider '{options.Provider}'. Supported providers are GoogleAI and AzureOpenAI.")
        };
    }
}

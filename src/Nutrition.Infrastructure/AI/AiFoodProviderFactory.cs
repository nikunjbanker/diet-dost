/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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

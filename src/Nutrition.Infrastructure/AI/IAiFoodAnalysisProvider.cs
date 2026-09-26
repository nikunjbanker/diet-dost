/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

public interface IAiFoodAnalysisProvider
{
    string ProviderName { get; }
    bool SupportsVision { get; }

    Task<IndianMealAnalysisResult?> AnalyzePhotoAsync(
        byte[] imageBytes,
        string mimeType,
        string prompt,
        string modelId,
        CancellationToken ct);

    Task<IndianMealAnalysisResult?> AnalyzeTextAsync(
        string prompt,
        string modelId,
        CancellationToken ct);
}

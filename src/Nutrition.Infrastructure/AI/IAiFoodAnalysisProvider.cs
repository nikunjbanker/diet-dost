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

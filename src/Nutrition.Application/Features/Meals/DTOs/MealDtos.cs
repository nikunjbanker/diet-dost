using Nutrition.Application.Agents;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.DTOs;

public record MealAnalysisResultDto(
    bool ConfidenceGated,
    double ConfidenceScore,
    string? DetectedByModel,
    string? PhotoUrl,
    string? Message,
    string? Advice,
    bool RequiresRetake,
    IndianMealAnalysisResult? Analysis
);

public record ConfirmMealResultDto(
    MealLog Meal,
    DailyCalorieLedger DailyLedger,
    List<string> LearnedNotes,
    string? LearnedMessage,
    string Message
);

public record UpdateMealResultDto(
    MealLog Meal,
    DailyCalorieLedger DailyLedger,
    string Message
);

public record DeleteMealResultDto(
    bool Success,
    string Message
);

using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Commands.SubmitFeedback;

public record SubmitAiFeedbackCommand(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper,
    string? MealLogId,
    string DishName,
    string DetectedByModel,
    double ConfidenceScore,
    string Rating,
    string? Remarks,
    List<IndianMealItemDto>? Items
) : ICommand<Result<AiFeedbackResponseDto>>;

public record AiFeedbackResponseDto(
    bool Success,
    bool Retrained,
    string Message,
    string? OriginalDetectedDish,
    string? CorrectedDish,
    IndianMealItemDto? UpdatedItemEstimate
);

public class SubmitAiFeedbackCommandHandler : ICommandHandler<SubmitAiFeedbackCommand, Result<AiFeedbackResponseDto>>
{
    private readonly IFoodVisionAgent _visionAgent;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IRepository<AiDetectionFeedbackRecord> _feedbackRepo;
    private readonly IUnitOfWork _uow;

    public SubmitAiFeedbackCommandHandler(
        IFoodVisionAgent visionAgent,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IRepository<AiDetectionFeedbackRecord> feedbackRepo,
        IUnitOfWork uow)
    {
        _visionAgent = visionAgent;
        _correctionsRepo = correctionsRepo;
        _feedbackRepo = feedbackRepo;
        _uow = uow;
    }

    public async Task<Result<AiFeedbackResponseDto>> HandleAsync(SubmitAiFeedbackCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<AiFeedbackResponseDto>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        using var activity = NutritionTelemetry.ActivitySource.StartActivity("diet.ai_feedback", System.Diagnostics.ActivityKind.Server);
        activity?.SetTag("diet.feedback.rating", request.Rating);
        activity?.SetTag("diet.feedback.model", request.DetectedByModel);
        activity?.SetTag("diet.feedback.dish", request.DishName);

        var feedbackRecord = new AiDetectionFeedbackRecord
        {
            UserId = effectiveUserId,
            MealLogId = request.MealLogId,
            DishName = request.DishName,
            DetectedByModel = request.DetectedByModel,
            ConfidenceScore = request.ConfidenceScore,
            Rating = request.Rating,
            Remarks = request.Remarks,
            IdentifiedItemsSummary = request.Items != null ? string.Join("; ", request.Items.Select(i => $"{i.Name} ({i.EstimatedPortion})")) : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        var retrainingResult = await _visionAgent.ProcessFeedbackRetrainingAsync(
            effectiveUserId,
            request.DishName,
            request.Rating,
            request.Remarks,
            request.Items,
            ct);

        feedbackRecord.RetrainingTriggered = retrainingResult.Retrained;
        feedbackRecord.RetrainingOutcome = retrainingResult.Message;

        if (retrainingResult.Retrained &&
            !string.IsNullOrWhiteSpace(retrainingResult.OriginalDetectedDish) &&
            !string.IsNullOrWhiteSpace(retrainingResult.CorrectedDish) &&
            !retrainingResult.OriginalDetectedDish.Equals(retrainingResult.CorrectedDish, StringComparison.OrdinalIgnoreCase))
        {
            var origKey = retrainingResult.OriginalDetectedDish.Trim();
            var correctedKey = retrainingResult.CorrectedDish.Trim();

            var contradictory = await _correctionsRepo.FindAsync(
                c => c.UserId == effectiveUserId &&
                     ((c.OriginalDetectedItem.ToLower() == correctedKey.ToLower() && c.CorrectedItemName.ToLower() == origKey.ToLower()) ||
                      (c.OriginalDetectedItem.ToLower().Contains(correctedKey.ToLower()) && c.CorrectedItemName.ToLower().Contains(origKey.ToLower()))),
                ct);

            foreach (var contra in contradictory)
            {
                await _correctionsRepo.DeleteAsync(contra.Id, ct);
            }

            var existing = (await _correctionsRepo.FindAsync(
                c => c.UserId == effectiveUserId && c.OriginalDetectedItem.ToLower() == origKey.ToLower(), ct))
                .FirstOrDefault();

            var updatedEstimate = retrainingResult.UpdatedItemEstimate;

            if (existing != null)
            {
                existing.CorrectedItemName = correctedKey;
                existing.HindiOrRegionalName = updatedEstimate?.HindiOrRegionalName ?? correctedKey;
                existing.EstimatedPortion = updatedEstimate?.EstimatedPortion ?? "1 Katori";
                existing.Calories = updatedEstimate?.Calories ?? 120;
                existing.ProteinGrams = updatedEstimate?.ProteinGrams ?? 3;
                existing.CarbsGrams = updatedEstimate?.CarbsGrams ?? 10;
                existing.FatGrams = updatedEstimate?.FatGrams ?? 6;
                existing.CreatedAtUtc = DateTime.UtcNow;
                existing.FrequencyCount++;
                await _correctionsRepo.UpdateAsync(existing, ct);
            }
            else
            {
                var newCorrection = new UserCorrectionRecord
                {
                    UserId = effectiveUserId,
                    OriginalDetectedItem = origKey,
                    CorrectedItemName = correctedKey,
                    HindiOrRegionalName = updatedEstimate?.HindiOrRegionalName ?? correctedKey,
                    EstimatedPortion = updatedEstimate?.EstimatedPortion ?? "1 Katori",
                    Calories = updatedEstimate?.Calories ?? 120,
                    ProteinGrams = updatedEstimate?.ProteinGrams ?? 3,
                    CarbsGrams = updatedEstimate?.CarbsGrams ?? 10,
                    FatGrams = updatedEstimate?.FatGrams ?? 6,
                    MealType = "Lunch",
                    CreatedAtUtc = DateTime.UtcNow,
                    FrequencyCount = 1
                };
                await _correctionsRepo.AddAsync(newCorrection, ct);
            }
        }

        await _feedbackRepo.AddAsync(feedbackRecord, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AiFeedbackResponseDto>.Success(new AiFeedbackResponseDto(
            true,
            retrainingResult.Retrained,
            retrainingResult.Message,
            retrainingResult.OriginalDetectedDish,
            retrainingResult.CorrectedDish,
            retrainingResult.UpdatedItemEstimate
        ));
    }
}

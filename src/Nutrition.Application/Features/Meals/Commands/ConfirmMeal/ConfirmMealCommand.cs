using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Commands.ConfirmMeal;

public record ConfirmMealCommand(
    MealLog Meal,
    string CurrentUserId,
    bool IsAdminOrSuper
) : ICommand<Result<ConfirmMealResultDto>>;

public class ConfirmMealCommandHandler : ICommandHandler<ConfirmMealCommand, Result<ConfirmMealResultDto>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IUnitOfWork _uow;

    public ConfirmMealCommandHandler(
        ClinicalDietitianService dietitianService,
        IRepository<UserCorrectionRecord> correctionsRepo,
        IUnitOfWork uow)
    {
        _dietitianService = dietitianService;
        _correctionsRepo = correctionsRepo;
        _uow = uow;
    }

    public async Task<Result<ConfirmMealResultDto>> HandleAsync(ConfirmMealCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<ConfirmMealResultDto>.Unauthorized();

        var meal = request.Meal;
        if (meal == null)
            return Result<ConfirmMealResultDto>.Failure("Meal payload cannot be null.", "InvalidPayload", 400);

        // Tenant isolation: meal belongs to authenticated user unless admin explicitly overrides
        if (!request.IsAdminOrSuper)
        {
            meal.UserId = request.CurrentUserId;
        }
        else if (string.IsNullOrWhiteSpace(meal.UserId))
        {
            meal.UserId = request.CurrentUserId;
        }

        meal.IsVerifiedByUser = true;
        meal.LoggedAt = meal.LoggedAt != default ? meal.LoggedAt.ToUniversalTime() : DateTime.UtcNow;

        // Continuous Model Re-Training & Adaptive Learning
        var learnedNotes = new List<string>();
        foreach (var item in meal.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.OriginalDetection) &&
                !item.OriginalDetection.Trim().Equals(item.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var origKey = item.OriginalDetection.Trim();
                var correctedKey = item.Name.Trim();

                if (origKey.StartsWith("Added by", StringComparison.OrdinalIgnoreCase) ||
                    origKey.Contains("Added by", StringComparison.OrdinalIgnoreCase) ||
                    correctedKey.StartsWith("Added by", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Purge contradictory rules
                var contradictory = await _correctionsRepo.FindAsync(
                    c => c.UserId == meal.UserId &&
                         ((c.OriginalDetectedItem.ToLower() == correctedKey.ToLower() && c.CorrectedItemName.ToLower() == origKey.ToLower()) ||
                          (c.OriginalDetectedItem.ToLower().Contains(correctedKey.ToLower()) && c.CorrectedItemName.ToLower().Contains(origKey.ToLower()))),
                    ct);

                foreach (var contra in contradictory)
                {
                    await _correctionsRepo.DeleteAsync(contra.Id, ct);
                }

                var existing = (await _correctionsRepo.FindAsync(
                    c => c.UserId == meal.UserId && c.OriginalDetectedItem.ToLower() == origKey.ToLower(), ct))
                    .FirstOrDefault();

                if (existing != null)
                {
                    existing.CorrectedItemName = correctedKey;
                    existing.HindiOrRegionalName = item.HindiOrRegionalName;
                    existing.EstimatedPortion = item.EstimatedPortion;
                    existing.Calories = item.Calories;
                    existing.ProteinGrams = item.ProteinGrams;
                    existing.CarbsGrams = item.CarbsGrams;
                    existing.FatGrams = item.FatGrams;
                    existing.MealType = meal.MealType.ToString();
                    existing.CreatedAtUtc = DateTime.UtcNow;
                    existing.FrequencyCount++;
                    await _correctionsRepo.UpdateAsync(existing, ct);
                }
                else
                {
                    var newCorrection = new UserCorrectionRecord
                    {
                        UserId = meal.UserId,
                        OriginalDetectedItem = origKey,
                        CorrectedItemName = correctedKey,
                        HindiOrRegionalName = item.HindiOrRegionalName,
                        EstimatedPortion = item.EstimatedPortion,
                        Calories = item.Calories,
                        ProteinGrams = item.ProteinGrams,
                        CarbsGrams = item.CarbsGrams,
                        FatGrams = item.FatGrams,
                        MealType = meal.MealType.ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        FrequencyCount = 1
                    };
                    await _correctionsRepo.AddAsync(newCorrection, ct);
                }

                learnedNotes.Add($"Diet Dost learned: '{origKey}' ➔ '{correctedKey}'");
            }
        }

        if (learnedNotes.Count > 0)
        {
            await _uow.SaveChangesAsync(ct);
        }

        meal.RecalculateTotals();
        var saved = await _dietitianService.LogMealAsync(meal, ct);
        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(meal.UserId, DateOnly.FromDateTime(meal.LoggedAt), ct);

        return Result<ConfirmMealResultDto>.Success(new ConfirmMealResultDto(
            saved,
            ledger,
            learnedNotes,
            learnedNotes.Count > 0 ? string.Join(". ", learnedNotes) : null,
            "Meal logged successfully! Confetti burst triggered 🎉"
        ));
    }
}

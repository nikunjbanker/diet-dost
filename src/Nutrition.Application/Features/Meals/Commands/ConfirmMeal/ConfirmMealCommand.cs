/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Common.Security;
using Nutrition.Application.Features.Analytics.DTOs;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Commands.ConfirmMeal;

public record ConfirmMealCommand(
    SaveMealRequestDto Meal,
    string CurrentUserId,
    bool IsAdminOrSuper,
    string? ClientMutationId = null,
    DateTime? ClientTimestampUtc = null
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

        var mealDto = request.Meal;
        if (mealDto == null)
            return Result<ConfirmMealResultDto>.Failure("Meal payload cannot be null.", "InvalidPayload", 400);

        // Tenant isolation: meal belongs to authenticated user unless admin explicitly overrides
        var effectiveUserId = request.IsAdminOrSuper && !string.IsNullOrWhiteSpace(mealDto.UserId)
            ? mealDto.UserId
            : request.CurrentUserId;

        var meal = mealDto.ToNewDomainEntity(effectiveUserId);
        if (!string.IsNullOrWhiteSpace(request.ClientMutationId))
        {
            meal.ClientMutationId ??= request.ClientMutationId;
        }
        if (request.ClientTimestampUtc.HasValue)
        {
            meal.ClientTimestampUtc ??= request.ClientTimestampUtc.Value;
        }

        if (!string.IsNullOrWhiteSpace(meal.PhotoUri))
        {
            meal.PhotoUri = PhotoPathSanitizer.NormalizeToRelativePath(meal.PhotoUri);
        }

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

                var normalizedOrig = UserCorrectionRecord.NormalizeItemKey(origKey);
                var normalizedCorrected = UserCorrectionRecord.NormalizeItemKey(correctedKey);

                // Purge contradictory rules using bounded user corrections
                var userCorrections = await _correctionsRepo.FindAsync(c => c.UserId == meal.UserId, ct);
                var contradictory = userCorrections.Where(c =>
                    (c.NormalizedOriginalItem == normalizedCorrected && c.CorrectedItemName.Equals(origKey, StringComparison.OrdinalIgnoreCase)) ||
                    (c.OriginalDetectedItem.Contains(correctedKey, StringComparison.OrdinalIgnoreCase) && c.CorrectedItemName.Contains(origKey, StringComparison.OrdinalIgnoreCase)));

                foreach (var contra in contradictory)
                {
                    await _correctionsRepo.DeleteAsync(contra.Id, ct);
                }

                var existing = (await _correctionsRepo.FindAsync(
                    c => c.UserId == meal.UserId && c.NormalizedOriginalItem == normalizedOrig, ct))
                    .FirstOrDefault()
                    ?? userCorrections.FirstOrDefault(c => c.OriginalDetectedItem.Equals(origKey, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.NormalizedOriginalItem = normalizedOrig;
                    existing.CorrectedItemName = correctedKey;
                    existing.HindiOrRegionalName = item.HindiOrRegionalName;
                    existing.EstimatedPortion = item.EstimatedPortion;
                    existing.Calories = item.Calories;
                    existing.ProteinGrams = item.ProteinGrams;
                    existing.CarbsGrams = item.CarbsGrams;
                    existing.FatGrams = item.FatGrams;
                    existing.MealType = meal.MealType.ToString();
                    existing.CreatedAtUtc = DateTime.UtcNow;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    existing.FrequencyCount++;
                    await _correctionsRepo.UpdateAsync(existing, ct);
                }
                else
                {
                    var newCorrection = new UserCorrectionRecord
                    {
                        UserId = meal.UserId,
                        OriginalDetectedItem = origKey,
                        NormalizedOriginalItem = normalizedOrig,
                        CorrectedItemName = correctedKey,
                        HindiOrRegionalName = item.HindiOrRegionalName,
                        EstimatedPortion = item.EstimatedPortion,
                        Calories = item.Calories,
                        ProteinGrams = item.ProteinGrams,
                        CarbsGrams = item.CarbsGrams,
                        FatGrams = item.FatGrams,
                        MealType = meal.MealType.ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow,
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
            saved.ToDto(),
            ledger.ToDto(),
            learnedNotes,
            learnedNotes.Count > 0 ? string.Join(". ", learnedNotes) : null,
            "Meal logged successfully! Confetti burst triggered 🎉"
        ));
    }
}

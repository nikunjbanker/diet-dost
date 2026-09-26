/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Commands.UpdateMeal;

public record UpdateMealCommand(
    string Id,
    MealLog Meal,
    string CurrentUserId,
    bool IsAdminOrSuper
) : ICommand<Result<UpdateMealResultDto>>;

public class UpdateMealCommandHandler : ICommandHandler<UpdateMealCommand, Result<UpdateMealResultDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public UpdateMealCommandHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<UpdateMealResultDto>> HandleAsync(UpdateMealCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<UpdateMealResultDto>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Id))
            return Result<UpdateMealResultDto>.Failure("Meal id is required.", "MissingId", 400);

        if (request.Meal == null)
            return Result<UpdateMealResultDto>.Failure("Meal payload cannot be null.", "InvalidPayload", 400);

        var existing = await _dietitianService.GetMealByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<UpdateMealResultDto>.NotFound($"Meal with ID '{request.Id}' was not found.");

        if (existing.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
            return Result<UpdateMealResultDto>.Forbidden("Access denied to update another user's meal.");

        request.Meal.Id = request.Id;
        request.Meal.UserId = existing.UserId;

        var updated = await _dietitianService.UpdateMealAsync(request.Meal, ct);
        if (updated == null)
            return Result<UpdateMealResultDto>.NotFound($"Meal with ID '{request.Id}' was not found.");

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(updated.UserId, DateOnly.FromDateTime(updated.LoggedAt), ct);

        return Result<UpdateMealResultDto>.Success(new UpdateMealResultDto(
            updated,
            ledger,
            "Meal updated successfully! Daily ledger and trends synchronized ✨"
        ));
    }
}

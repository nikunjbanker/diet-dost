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

namespace Nutrition.Application.Features.Meals.Commands.DeleteMeal;

public record DeleteMealCommand(
    string Id,
    string CurrentUserId,
    bool IsAdminOrSuper
) : ICommand<Result<DeleteMealResultDto>>;

public class DeleteMealCommandHandler : ICommandHandler<DeleteMealCommand, Result<DeleteMealResultDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public DeleteMealCommandHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<DeleteMealResultDto>> HandleAsync(DeleteMealCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<DeleteMealResultDto>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Id))
            return Result<DeleteMealResultDto>.Failure("Meal id is required.", "MissingId", 400);

        var existing = await _dietitianService.GetMealByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<DeleteMealResultDto>.NotFound($"Meal with ID '{request.Id}' was not found.");

        if (existing.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
            return Result<DeleteMealResultDto>.Forbidden("Access denied to delete another user's meal.");

        var success = await _dietitianService.DeleteMealAsync(request.Id, ct);
        if (!success)
            return Result<DeleteMealResultDto>.NotFound($"Meal with ID '{request.Id}' was not found.");

        return Result<DeleteMealResultDto>.Success(new DeleteMealResultDto(
            true,
            "Meal deleted successfully! Daily ledger recalculated 🗑️"
        ));
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Profile.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Profile.Commands.SaveProfile;

public record SaveProfileCommand(
    UserProfile Profile,
    string CurrentUserId
) : ICommand<Result<ProfileResponseDto>>;

public class SaveProfileCommandHandler : ICommandHandler<SaveProfileCommand, Result<ProfileResponseDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public SaveProfileCommandHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<ProfileResponseDto>> HandleAsync(SaveProfileCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<ProfileResponseDto>.Unauthorized();

        if (request.Profile == null)
            return Result<ProfileResponseDto>.Failure("Profile payload cannot be null.", "InvalidPayload", 400);

        request.Profile.Id = request.CurrentUserId;

        try
        {
            var saved = await _dietitianService.SaveProfileAsync(request.Profile, ct);
            var budget = _dietitianService.CalculateTargetBudget(saved);
            var macros = _dietitianService.CalculateMacros(saved);

            return Result<ProfileResponseDto>.Success(new ProfileResponseDto(
                saved,
                budget,
                macros,
                "Clinical profile saved successfully. Caloric budget and macronutrients calculated per ICMR-NIN & WHO standards."
            ));
        }
        catch (InvalidOperationException ex)
        {
            return Result<ProfileResponseDto>.Failure(ex.Message, "ZeroAssumptionViolation", 400);
        }
    }
}

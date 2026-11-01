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
using Nutrition.Application.Features.Profile.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Profile.Commands.SaveProfile;

public record SaveProfileCommand(
    UserProfile Profile,
    string CurrentUserId
) : ICommand<Result<ProfileResponseDto>>;

public class SaveProfileCommandHandler : ICommandHandler<SaveProfileCommand, Result<ProfileResponseDto>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IRepository<ApplicationUser>? _userRepo;
    private readonly IUnitOfWork? _uow;

    public SaveProfileCommandHandler(
        ClinicalDietitianService dietitianService,
        IRepository<ApplicationUser>? userRepo = null,
        IUnitOfWork? uow = null)
    {
        _dietitianService = dietitianService;
        _userRepo = userRepo;
        _uow = uow;
    }

    public SaveProfileCommandHandler(ClinicalDietitianService dietitianService)
        : this(dietitianService, null, null)
    {
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
            if (_userRepo != null && _uow != null)
            {
                var user = await _userRepo.GetByIdAsync(request.CurrentUserId, ct);
                if (user != null && !user.IsOnboarded)
                {
                    user.IsOnboarded = true;
                    await _userRepo.UpdateAsync(user, ct);
                    await _uow.SaveChangesAsync(ct);
                }
            }
            var budget = ClinicalCalculators.CalculateCaloricBudget(saved);
            var macros = ClinicalCalculators.CalculateMacroDistribution(saved, budget);

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

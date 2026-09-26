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

namespace Nutrition.Application.Features.Profile.Queries.GetProfile;

public record GetProfileQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper
) : IQuery<Result<ProfileResponseDto>>;

public class GetProfileQueryHandler : IQueryHandler<GetProfileQuery, Result<ProfileResponseDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public GetProfileQueryHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<ProfileResponseDto>> HandleAsync(GetProfileQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<ProfileResponseDto>.Unauthorized();

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<ProfileResponseDto>.Forbidden("Access denied to inspect another user's health profile.");
        }

        var profile = await _dietitianService.GetProfileAsync(targetUserId, ct);
        if (profile == null)
        {
            return Result<ProfileResponseDto>.NotFound("User profile not found. Please complete clinical onboarding.");
        }

        var budget = _dietitianService.CalculateTargetBudget(profile);
        var macros = _dietitianService.CalculateMacros(profile);

        return Result<ProfileResponseDto>.Success(new ProfileResponseDto(profile, budget, macros));
    }
}

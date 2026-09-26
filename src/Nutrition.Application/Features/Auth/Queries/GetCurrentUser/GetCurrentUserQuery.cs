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
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery(string UserId) : IQuery<Result<CurrentUserResponseDto>>;

public class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponseDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;

    public GetCurrentUserQueryHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<TierFeatureConfiguration> tierRepo)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _tierRepo = tierRepo;
    }

    public async Task<Result<CurrentUserResponseDto>> HandleAsync(GetCurrentUserQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<CurrentUserResponseDto>.Unauthorized();

        var user = await _userRepo.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return Result<CurrentUserResponseDto>.NotFound("User account not found.");

        var profile = await _profileRepo.GetByIdAsync(request.UserId, ct);
        var displayName = profile?.Name ?? user.Email;
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        var responseDto = new CurrentUserResponseDto(
            user.Id,
            user.Email,
            user.MobileNumber,
            displayName,
            user.Role,
            user.Tier,
            user.IsEmailVerified,
            user.IsMobileVerified,
            tierConfig
        );

        return Result<CurrentUserResponseDto>.Success(responseDto);
    }
}

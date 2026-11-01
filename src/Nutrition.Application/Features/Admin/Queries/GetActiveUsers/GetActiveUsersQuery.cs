/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Admin.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Admin.Queries.GetActiveUsers;

/// <summary>
/// CQRS Query retrieving active users within a sliding time window.
/// Strictly restricted to Admin and SuperAdmin callers.
/// </summary>
public record GetActiveUsersQuery(
    int WindowMinutes = 15
) : IQuery<Result<List<ActiveUserDto>>>;

/// <summary>
/// Handler for <see cref="GetActiveUsersQuery"/>.
/// Queries indexed LastActiveUtc timestamps while enforcing soft-delete and DPDPA filters.
/// </summary>
public class GetActiveUsersQueryHandler : IQueryHandler<GetActiveUsersQuery, Result<List<ActiveUserDto>>>
{
    private readonly IRepository<ApplicationUser> _userRepo;

    public GetActiveUsersQueryHandler(IRepository<ApplicationUser> userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<Result<List<ActiveUserDto>>> HandleAsync(GetActiveUsersQuery request, CancellationToken ct = default)
    {
        var window = request.WindowMinutes <= 0 ? 15 : request.WindowMinutes;
        var now = DateTime.UtcNow;
        var cutoffUtc = now.AddMinutes(-window);
        var onlineThresholdUtc = now.AddMinutes(-5);

        var activeUsers = await _userRepo.FindAsync(
            u => u.LastActiveUtc.HasValue && u.LastActiveUtc.Value >= cutoffUtc && !u.IsDeleted && u.IsActive,
            ct);

        var dtos = activeUsers
            .OrderByDescending(u => u.LastActiveUtc)
            .Select(u => new ActiveUserDto(
                Id: u.Id,
                Email: u.Email,
                Role: u.Role,
                Tier: u.Tier,
                LastActiveUtc: u.LastActiveUtc,
                IsCurrentlyOnline: u.LastActiveUtc.HasValue && u.LastActiveUtc.Value >= onlineThresholdUtc
            ))
            .ToList();

        return Result<List<ActiveUserDto>>.Success(dtos);
    }
}

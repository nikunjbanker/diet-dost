/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Profile.Commands.SaveProfile;
using Nutrition.Application.Features.Profile.Queries.GetProfile;
using Nutrition.Domain.Model.Profile;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// Thin Presentation Controller for Clinical Profiles.
/// Dispatches profile retrieval and onboarding persistence to Application CQRS handlers.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProfileController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("{userId?}")]
    public async Task<IActionResult> GetProfile(string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetProfileQuery(currentUserId, userId, User.IsAdminOrSuper());
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { message = result.Error, error = result.ErrorCode });
        }

        return Ok(new
        {
            profile = result.Data!.Profile,
            budget = result.Data.Budget,
            macros = result.Data.Macros
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveProfile([FromBody] UserProfile profile, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new SaveProfileCommand(profile, currentUserId);
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                error = result.Error,
                zeroAssumptionViolation = result.ErrorCode == "ZeroAssumptionViolation"
            });
        }

        return Ok(new
        {
            profile = result.Data!.Profile,
            budget = result.Data.Budget,
            macros = result.Data.Macros,
            message = result.Data.Message
        });
    }
}

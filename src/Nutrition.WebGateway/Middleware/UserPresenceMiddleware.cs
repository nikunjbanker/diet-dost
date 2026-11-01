/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Middleware;

/// <summary>
/// ASP.NET Core middleware that records authenticated user interactions on the active presence tracker.
/// Placed immediately after UseAuthentication() and UseAuthorization() in the HTTP pipeline.
/// </summary>
public class UserPresenceMiddleware
{
    private readonly RequestDelegate _next;

    public UserPresenceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserPresenceTracker presenceTracker)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.GetUserId();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var tierStr = context.User.GetTier();
                var tier = Enum.TryParse<UserTier>(tierStr, true, out var parsedTier) ? parsedTier : UserTier.Free;

                var roleStr = context.User.GetRole();
                var role = Enum.TryParse<UserRole>(roleStr, true, out var parsedRole) ? parsedRole : UserRole.User;

                presenceTracker.TrackActivity(userId, tier, role);
            }
        }

        await _next(context);
    }
}

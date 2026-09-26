/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Http;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Services;

/// <summary>
/// WebGateway adapter resolving authenticated user identity from the ambient HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.GetUserId();

    public string? Email => _httpContextAccessor.HttpContext?.User.GetEmail();

    public UserRole Role
    {
        get
        {
            var roleStr = _httpContextAccessor.HttpContext?.User.GetRole();
            return Enum.TryParse<UserRole>(roleStr, true, out var parsedRole) ? parsedRole : UserRole.User;
        }
    }

    public UserTier Tier
    {
        get
        {
            var tierStr = _httpContextAccessor.HttpContext?.User.GetTier();
            return Enum.TryParse<UserTier>(tierStr, true, out var parsedTier) ? parsedTier : UserTier.Free;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool IsAdminOrSuper => _httpContextAccessor.HttpContext?.User.IsAdminOrSuper() ?? false;
}

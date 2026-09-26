/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Port abstraction for accessing the ambient authenticated user context,
/// shielding Application layer handlers from ASP.NET Core HttpContext / ClaimsPrincipal coupling.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    UserRole Role { get; }
    UserTier Tier { get; }
    bool IsAuthenticated { get; }
    bool IsAdminOrSuper { get; }
}

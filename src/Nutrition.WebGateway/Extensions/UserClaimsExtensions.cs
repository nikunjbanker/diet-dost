/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Cryptographic claims extraction and ClaimsPrincipal materialization helpers for authenticated users.
/// Eliminates insecure client-supplied identity parameters (OWASP A01: Broken Access Control).
/// </summary>
public static class UserClaimsExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("userId")?.Value;
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value;
    }

    public static string? GetTier(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("tier")?.Value;
    }

    public static bool IsAdminOrSuper(this ClaimsPrincipal principal)
    {
        var role = principal.GetRole();
        return role is nameof(UserRole.Admin) or nameof(UserRole.SuperAdmin);
    }

    public static ClaimsPrincipal ToClaimsPrincipal(this AuthenticatedClaimsPayload payload)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload.UserId),
            new(ClaimTypes.Name, payload.DisplayName),
            new(ClaimTypes.Email, payload.Email),
            new(ClaimTypes.Role, payload.Role),
            new("tier", payload.Tier),
            new("security_stamp", payload.SecurityStamp)
        };

        if (payload.IsAnonymous)
        {
            claims.Add(new("is_anonymous", "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, payload.AuthenticationScheme));
    }
}

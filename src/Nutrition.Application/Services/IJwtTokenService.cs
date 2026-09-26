/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Services;

/// <summary>
/// Service contract for generating and validating cryptographically signed JSON Web Tokens (JWT).
/// Adheres to OWASP ASVS token standards (HMAC-SHA256, explicit subject/role/tier claims).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT token containing user identity, role, and tier claims.
    /// </summary>
    /// <param name="user">The authenticated application user.</param>
    /// <param name="displayName">Optional user display name.</param>
    /// <returns>Base64Url encoded JWT bearer token.</returns>
    string GenerateToken(ApplicationUser user, string? displayName = null);

    /// <summary>
    /// Validates a raw JWT bearer token string and returns the extracted ClaimsPrincipal if valid.
    /// </summary>
    /// <param name="token">The JWT token string.</param>
    /// <returns>ClaimsPrincipal if signature and lifetime are valid, otherwise null.</returns>
    ClaimsPrincipal? ValidateToken(string token);
}

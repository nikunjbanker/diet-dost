/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common;

/// <summary>
/// Cryptographic password hashing contract adhering to OWASP Password Storage standards.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a raw password with a unique cryptographic salt and high-iteration derivation function.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a raw password against an existing secure hash representation.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}

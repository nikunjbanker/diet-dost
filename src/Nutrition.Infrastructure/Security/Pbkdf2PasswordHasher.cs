/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Security;

/// <summary>
/// Enterprise-grade password hasher utilizing PBKDF2 with HMAC-SHA512 per OWASP Password Storage guidelines.
/// Formats hashes with explicit version, salt, and iteration parameters for forward-compatible key derivation.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;       // 128 bits
    private const int SubkeySizeBytes = 32;     // 256 bits
    private const int IterationCount = 100_000; // OWASP recommendation for PBKDF2-HMAC-SHA512
    private const string HashHeader = "PBKDF2$SHA512";

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA512,
            SubkeySizeBytes);

        var saltBase64 = Convert.ToBase64String(salt);
        var subkeyBase64 = Convert.ToBase64String(subkey);

        return $"{HashHeader}${IterationCount}${saltBase64}${subkeyBase64}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        var parts = passwordHash.Split('$');
        if (parts.Length != 5)
            return false;

        var prefix = $"{parts[0]}${parts[1]}";
        if (prefix != HashHeader)
            return false;

        if (!int.TryParse(parts[2], out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expectedSubkey = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            expectedSubkey.Length);

        // Constant-time comparison to prevent timing side-channel attacks (OWASP A02)
        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using Nutrition.Application.Common;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Infrastructure.Security;

/// <summary>
/// Cryptographic implementation of IOtpService.
/// Generates unbiased random 6-digit codes and builds hashed VerificationOtp aggregate records.
/// </summary>
public class OtpService : IOtpService
{
    public string GenerateCode()
    {
        // Thread-safe, unbiased cryptographic random integer between 100000 and 999999 inclusive
        var codeInt = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        return codeInt.ToString("D6");
    }

    public VerificationOtp CreateOtp(
        string userId,
        string target,
        OtpChannel channel,
        string rawCode,
        int validityMinutes = VerificationOtp.OtpValidityMinutes)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            throw new ArgumentException("Raw code cannot be empty.", nameof(rawCode));

        var hash = VerificationOtp.ComputeHash(rawCode);
        return new VerificationOtp
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Target = target.Trim(),
            OtpCodeHash = hash,
            Channel = channel,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(validityMinutes),
            AttemptCount = 0,
            IsUsed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public bool ValidateCode(VerificationOtp otp, string inputCode, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(otp);
        return otp.Verify(inputCode, out failureReason);
    }
}

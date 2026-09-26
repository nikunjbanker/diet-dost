/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common;

/// <summary>
/// Service contract for generating and validating cryptographically secure One-Time Passwords (OTPs).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a cryptographically random 6-digit numeric verification code.
    /// </summary>
    string GenerateCode();

    /// <summary>
    /// Creates a time-bounded VerificationOtp aggregate with SHA-256 hashed code.
    /// </summary>
    VerificationOtp CreateOtp(string userId, string target, OtpChannel channel, string rawCode, int validityMinutes = VerificationOtp.OtpValidityMinutes);

    /// <summary>
    /// Validates an input OTP code against the verification record in constant time.
    /// </summary>
    bool ValidateCode(VerificationOtp otp, string inputCode, out string? failureReason);
}

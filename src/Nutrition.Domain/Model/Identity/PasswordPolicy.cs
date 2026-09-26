/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.RegularExpressions;

namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Centralised, immutable password policy for Diet Dost.
/// All registration and password-reset flows MUST validate through this class.
///
/// Policy (OWASP A07:2021 — Identification &amp; Authentication Failures):
///   • Minimum 10 characters, maximum 30 characters.
///   • At least one UPPERCASE letter (A–Z).
///   • At least one lowercase letter (a–z).
///   • At least one digit (0–9).
///   • At least one ALLOWED special character from: ! @ # $ % ^ &amp; * ( ) - _ = + [ ] { } | : , . ?
///   • Characters that aid injection / hacking attacks are explicitly BANNED:
///     &lt; &gt; ; &apos; " \ / ` ~ NUL and other control chars.
/// </summary>
public static partial class PasswordPolicy
{
    // ── Boundary constants ────────────────────────────────────────────────────
    public const int MinLength = 10;
    public const int MaxLength = 30;

    // ── Allowed special characters (safe set — excludes < > ; ' " \ / ` ~) ──
    // Expressed as a character-class fragment used inside the regex
    private const string AllowedSpecialChars = @"!@#\$%\^&\*\(\)\-_=\+\[\]\{\}\|:,\.\?";

    // ── Compiled regex patterns ───────────────────────────────────────────────

    /// <summary>Characters that are explicitly banned (injection-risk characters).</summary>
    [GeneratedRegex(@"[<>;'""\\\/`~\x00-\x1F\x7F]")]
    private static partial Regex BannedCharsRegex();

    /// <summary>Requires at least one uppercase ASCII letter.</summary>
    [GeneratedRegex(@"[A-Z]")]
    private static partial Regex UpperCaseRegex();

    /// <summary>Requires at least one lowercase ASCII letter.</summary>
    [GeneratedRegex(@"[a-z]")]
    private static partial Regex LowerCaseRegex();

    /// <summary>Requires at least one decimal digit.</summary>
    [GeneratedRegex(@"[0-9]")]
    private static partial Regex DigitRegex();

    /// <summary>Requires at least one allowed special character.</summary>
    [GeneratedRegex(@"[!@#\$%\^&\*\(\)\-_=\+\[\]\{\}\|:,\.\?]")]
    private static partial Regex AllowedSpecialCharRegex();

    /// <summary>
    /// Validates a plain-text password against the Diet Dost password policy.
    /// Returns <c>true</c> when the password is compliant; otherwise returns
    /// <c>false</c> with a user-friendly <paramref name="failureMessage"/>.
    /// </summary>
    /// <param name="password">Plain-text password to evaluate.</param>
    /// <param name="failureMessage">
    /// Human-readable reason for failure, or <c>null</c> on success.
    /// </param>
    public static bool IsValid(string? password, out string? failureMessage)
    {
        if (string.IsNullOrEmpty(password))
        {
            failureMessage = "Password is required.";
            return false;
        }

        if (password.Length < MinLength)
        {
            failureMessage = $"Password must be at least {MinLength} characters long.";
            return false;
        }

        if (password.Length > MaxLength)
        {
            failureMessage = $"Password must not exceed {MaxLength} characters.";
            return false;
        }

        if (BannedCharsRegex().IsMatch(password))
        {
            failureMessage = "Password contains disallowed characters. Characters like < > ; ' \" \\ / ` ~ are not permitted.";
            return false;
        }

        if (!UpperCaseRegex().IsMatch(password))
        {
            failureMessage = "Password must contain at least one uppercase letter (A–Z).";
            return false;
        }

        if (!LowerCaseRegex().IsMatch(password))
        {
            failureMessage = "Password must contain at least one lowercase letter (a–z).";
            return false;
        }

        if (!DigitRegex().IsMatch(password))
        {
            failureMessage = "Password must contain at least one digit (0–9).";
            return false;
        }

        if (!AllowedSpecialCharRegex().IsMatch(password))
        {
            failureMessage = "Password must contain at least one special character (e.g. ! @ # $ % & * - _ + . ?).";
            return false;
        }

        failureMessage = null;
        return true;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <paramref name="password"/>
    /// fails the policy check. Used in domain invariant enforcement.
    /// </summary>
    public static void EnforceOrThrow(string? password)
    {
        if (!IsValid(password, out var message))
            throw new InvalidOperationException(message!);
    }
}

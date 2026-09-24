using System.Security.Cryptography;
using System.Text;

namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Time-bounded, cryptographically secured One-Time Password (OTP) for account verification.
/// Protects against brute-force guessing via attempt ceilings and constant-time hash verification.
/// </summary>
public class VerificationOtp
{
    public const int MaxAttemptsAllowed = 3;
    public const int OtpValidityMinutes = 5;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Target destination of the OTP (normalized email address or E.164 phone number).
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the plain-text 6-digit verification code.
    /// Plain-text code is NEVER stored in the database.
    /// </summary>
    public string OtpCodeHash { get; set; } = string.Empty;

    /// <summary>
    /// Delivery channel (Email or SMS).
    /// </summary>
    public OtpChannel Channel { get; set; }

    /// <summary>
    /// Expiration timestamp in Universal UTC time.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Number of incorrect verification attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Flag indicating whether this OTP has been successfully verified.
    /// </summary>
    public bool IsUsed { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Generates a SHA-256 hash representation of a raw OTP string.
    /// </summary>
    public static string ComputeHash(string rawCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawCode.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether this OTP has passed its expiration window.
    /// </summary>
    public bool IsExpired(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return now >= ExpiresAtUtc;
    }

    /// <summary>
    /// Verifies an incoming plain-text OTP code against the stored hash in constant time.
    /// </summary>
    public bool Verify(string inputOtpCode, out string? failureReason, DateTime? utcNow = null)
    {
        if (IsUsed)
        {
            failureReason = "This verification code has already been used. Please request a new code.";
            return false;
        }

        if (IsExpired(utcNow))
        {
            failureReason = "Verification code has expired. Please request a new code.";
            return false;
        }

        if (AttemptCount >= MaxAttemptsAllowed)
        {
            failureReason = "Maximum verification attempts exceeded. Please request a new code.";
            return false;
        }

        var inputHash = ComputeHash(inputOtpCode);
        var storedHashBytes = Encoding.UTF8.GetBytes(OtpCodeHash);
        var inputHashBytes = Encoding.UTF8.GetBytes(inputHash);

        // Constant-time comparison to prevent timing attacks (OWASP A02/A04)
        var isMatch = CryptographicOperations.FixedTimeEquals(storedHashBytes, inputHashBytes);
        if (!isMatch)
        {
            AttemptCount++;
            var remainingAttempts = MaxAttemptsAllowed - AttemptCount;
            failureReason = remainingAttempts > 0
                ? $"Invalid verification code. {remainingAttempts} attempt(s) remaining."
                : "Invalid verification code. Maximum attempts reached. Please request a new code.";
            return false;
        }

        IsUsed = true;
        failureReason = null;
        return true;
    }
}

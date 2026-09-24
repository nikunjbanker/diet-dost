using System.Text.RegularExpressions;

namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Root aggregate entity representing an authenticated, registered application user.
/// Maintains identity credentials, roles, subscription tier, and forensic legal consent audit records.
/// </summary>
public partial class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User's primary email address (mandatory for registration and verification).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Upper-cased normalized email for case-insensitive indexing.
    /// </summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>
    /// User's mobile phone number (mandatory field at registration).
    /// </summary>
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Normalized phone digits for unique lookup.
    /// </summary>
    public string NormalizedMobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// PBKDF2 HMAC-SHA512 password hash with embedded salt.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Random cryptographic value updated whenever credentials or security properties change.
    /// Used to invalidate concurrent sessions on password reset.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Authorization role (User, Admin, SuperAdmin).
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Feature entitlement and AI quota tier (Free, Basic, Premium, SuperAdmin).
    /// </summary>
    public UserTier Tier { get; set; } = UserTier.Free;

    /// <summary>
    /// Whether email address has been verified via OTP. Mandatory to activate and log into account.
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// Whether mobile phone has been verified via SMS OTP (initially optional for cost management).
    /// </summary>
    public bool IsMobileVerified { get; set; }

    /// <summary>
    /// Whether this user account is active. Inactive/locked users cannot log in.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // =========================================================================
    // Legal Compliance & Forensic Audit Properties (India DPDPA 2023 & GDPR)
    // =========================================================================

    /// <summary>
    /// Timestamp when General Terms, Privacy Policy & AI Model Training / Partner Sharing Agreement was accepted.
    /// </summary>
    public DateTime? TermsAcceptedAtUtc { get; set; }

    /// <summary>
    /// Specific version string of the Terms of Service accepted (e.g. "v1.0-202609").
    /// </summary>
    public string? TermsVersionAccepted { get; set; }

    /// <summary>
    /// Timestamp when explicit Sensitive Personal Health Data Processing Consent was granted.
    /// </summary>
    public DateTime? HealthConsentAcceptedAtUtc { get; set; }

    /// <summary>
    /// Specific version string of the Health Consent accepted (e.g. "v1.0-202609").
    /// </summary>
    public string? HealthConsentVersionAccepted { get; set; }

    /// <summary>
    /// Client IPv4/IPv6 address recorded at the registration transaction boundary for legal evidentiary defense.
    /// </summary>
    public string? ConsentIpAddress { get; set; }

    /// <summary>
    /// Client browser/device User-Agent string recorded at the time of legal consent.
    /// </summary>
    public string? ConsentUserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>
    /// Normalizes email address for consistent, case-insensitive indexing.
    /// </summary>
    public static string NormalizeEmailAddress(string email)
    {
        return email.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalizes mobile phone number by stripping whitespace, dashes, and parentheses.
    /// </summary>
    public static string NormalizePhoneNumber(string phone)
    {
        return PhoneNumberDigitsRegex().Replace(phone.Trim(), "");
    }

    /// <summary>
    /// Enforces domain invariants and mandatory legal consent rules upon registration.
    /// </summary>
    public void ValidateRegistration()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@') || !Email.Contains('.'))
            throw new InvalidOperationException("A valid email address is mandatory for registration.");

        if (string.IsNullOrWhiteSpace(MobileNumber) || MobileNumber.Trim().Length < 8)
            throw new InvalidOperationException("A valid mobile phone number is mandatory for registration.");

        if (string.IsNullOrWhiteSpace(PasswordHash))
            throw new InvalidOperationException("Password hash must be generated before saving user.");

        if (!TermsAcceptedAtUtc.HasValue || string.IsNullOrWhiteSpace(TermsVersionAccepted))
            throw new InvalidOperationException("DPDPA Compliance Violation: Terms of Service & AI Model Training Agreement must be accepted.");

        if (!HealthConsentAcceptedAtUtc.HasValue || string.IsNullOrWhiteSpace(HealthConsentVersionAccepted))
            throw new InvalidOperationException("DPDPA Compliance Violation: Explicit Sensitive Health Data Consent must be granted.");
    }

    /// <summary>
    /// Determines whether the user is eligible to log in and access application data.
    /// </summary>
    public bool CanLogin(out string? rejectionReason)
    {
        if (!IsActive)
        {
            rejectionReason = "This account has been deactivated. Please contact support.";
            return false;
        }

        if (!IsEmailVerified)
        {
            rejectionReason = "Email verification is mandatory to access Diet Dost. Please verify your 6-digit OTP.";
            return false;
        }

        rejectionReason = null;
        return true;
    }

    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex PhoneNumberDigitsRegex();
}

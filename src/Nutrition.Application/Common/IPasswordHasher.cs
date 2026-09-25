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

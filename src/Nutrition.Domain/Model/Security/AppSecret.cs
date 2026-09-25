namespace Nutrition.Domain.Model.Security;

/// <summary>
/// Represents a security credential, cryptographic signing key, or provider API token
/// persisted in the database table rather than hardcoded in source code or configuration files.
/// </summary>
public class AppSecret
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

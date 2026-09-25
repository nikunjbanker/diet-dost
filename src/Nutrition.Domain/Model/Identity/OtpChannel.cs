namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Defines channels for delivering one-time verification passwords.
/// </summary>
public enum OtpChannel
{
    Email = 0,
    Sms = 1
}

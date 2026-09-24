using Nutrition.Domain.Model.Identity;
using Xunit;

namespace Nutrition.Domain.Tests;

public class IdentityDomainModelTests
{
    [Fact]
    public void ApplicationUser_ValidRegistration_PassesValidation()
    {
        var user = new ApplicationUser
        {
            Email = "user@example.com",
            NormalizedEmail = ApplicationUser.NormalizeEmailAddress("user@example.com"),
            MobileNumber = "+91 98765 43210",
            NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+91 98765 43210"),
            PasswordHash = "PBKDF2$SHA512$100000$dummySalt$dummyHash",
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609",
            ConsentIpAddress = "127.0.0.1",
            ConsentUserAgent = "TestRunner"
        };

        // Assert no exception thrown
        user.ValidateRegistration();
        Assert.Equal("USER@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("+919876543210", user.NormalizedMobileNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("missingdomain@")]
    public void ApplicationUser_InvalidEmail_ThrowsValidationException(string email)
    {
        var user = new ApplicationUser
        {
            Email = email,
            MobileNumber = "+919876543210",
            PasswordHash = "dummyHash",
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("valid email address", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void ApplicationUser_InvalidMobile_ThrowsValidationException(string phone)
    {
        var user = new ApplicationUser
        {
            Email = "user@example.com",
            MobileNumber = phone,
            PasswordHash = "dummyHash",
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("valid mobile phone number", ex.Message);
    }

    [Fact]
    public void ApplicationUser_MissingTermsConsent_ThrowsDpdpaViolationException()
    {
        var user = new ApplicationUser
        {
            Email = "user@example.com",
            MobileNumber = "+919876543210",
            PasswordHash = "dummyHash",
            TermsAcceptedAtUtc = null,
            TermsVersionAccepted = null,
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("Terms of Service", ex.Message);
    }

    [Fact]
    public void ApplicationUser_MissingHealthConsent_ThrowsDpdpaViolationException()
    {
        var user = new ApplicationUser
        {
            Email = "user@example.com",
            MobileNumber = "+919876543210",
            PasswordHash = "dummyHash",
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0",
            HealthConsentAcceptedAtUtc = null,
            HealthConsentVersionAccepted = null
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("Sensitive Health Data Consent", ex.Message);
    }

    [Fact]
    public void ApplicationUser_CanLogin_RequiresActiveAndEmailVerified()
    {
        var unverifiedUser = new ApplicationUser
        {
            IsActive = true,
            IsEmailVerified = false
        };
        Assert.False(unverifiedUser.CanLogin(out var unverifiedReason));
        Assert.Contains("Email verification is mandatory", unverifiedReason);

        var inactiveUser = new ApplicationUser
        {
            IsActive = false,
            IsEmailVerified = true
        };
        Assert.False(inactiveUser.CanLogin(out var inactiveReason));
        Assert.Contains("deactivated", inactiveReason);

        var validUser = new ApplicationUser
        {
            IsActive = true,
            IsEmailVerified = true
        };
        Assert.True(validUser.CanLogin(out var validReason));
        Assert.Null(validReason);
    }

    [Fact]
    public void VerificationOtp_SuccessfulVerification_MarksUsed()
    {
        var rawCode = "654321";
        var hash = VerificationOtp.ComputeHash(rawCode);

        var otp = new VerificationOtp
        {
            UserId = "user-1",
            Target = "user@example.com",
            OtpCodeHash = hash,
            Channel = OtpChannel.Email,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            IsUsed = false
        };

        var success = otp.Verify(rawCode, out var failureReason);
        Assert.True(success);
        Assert.Null(failureReason);
        Assert.True(otp.IsUsed);
    }

    [Fact]
    public void VerificationOtp_WrongCode_IncrementsAttemptsAndRejects()
    {
        var rawCode = "123456";
        var hash = VerificationOtp.ComputeHash(rawCode);

        var otp = new VerificationOtp
        {
            UserId = "user-1",
            Target = "user@example.com",
            OtpCodeHash = hash,
            Channel = OtpChannel.Email,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            IsUsed = false
        };

        var success = otp.Verify("999999", out var failureReason);
        Assert.False(success);
        Assert.Equal(1, otp.AttemptCount);
        Assert.Contains("2 attempt(s) remaining", failureReason);
        Assert.False(otp.IsUsed);
    }

    [Fact]
    public void VerificationOtp_MaxAttemptsExceeded_BlocksVerification()
    {
        var rawCode = "123456";
        var hash = VerificationOtp.ComputeHash(rawCode);

        var otp = new VerificationOtp
        {
            UserId = "user-1",
            Target = "user@example.com",
            OtpCodeHash = hash,
            Channel = OtpChannel.Email,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 3,
            IsUsed = false
        };

        var success = otp.Verify(rawCode, out var failureReason);
        Assert.False(success);
        Assert.Contains("Maximum verification attempts exceeded", failureReason);
    }

    [Fact]
    public void VerificationOtp_ExpiredOtp_RejectsVerification()
    {
        var rawCode = "123456";
        var hash = VerificationOtp.ComputeHash(rawCode);

        var otp = new VerificationOtp
        {
            UserId = "user-1",
            Target = "user@example.com",
            OtpCodeHash = hash,
            Channel = OtpChannel.Email,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1), // Expired
            AttemptCount = 0,
            IsUsed = false
        };

        var success = otp.Verify(rawCode, out var failureReason);
        Assert.False(success);
        Assert.Contains("expired", failureReason);
    }

    [Fact]
    public void TierFeatureConfiguration_Defaults_ConformToApprovedMatrix()
    {
        var defaults = TierFeatureConfiguration.GetDefaultConfigurations();

        var free = defaults.Single(t => t.Tier == UserTier.Free);
        Assert.Equal(1, free.DailyAiDetectionLimit);
        Assert.False(free.AllowPhotoCompare);
        Assert.False(free.AllowDataExport);
        Assert.Equal(7, free.AnalyticsHistoryDays);

        var basic = defaults.Single(t => t.Tier == UserTier.Basic);
        Assert.Equal(7, basic.DailyAiDetectionLimit);
        Assert.False(basic.AllowPhotoCompare);
        Assert.False(basic.AllowDataExport);
        Assert.Equal(30, basic.AnalyticsHistoryDays);

        var premium = defaults.Single(t => t.Tier == UserTier.Premium);
        Assert.Equal(30, premium.DailyAiDetectionLimit);
        Assert.True(premium.AllowPhotoCompare);
        Assert.True(premium.AllowDataExport);
        Assert.Equal(365, premium.AnalyticsHistoryDays);

        var superAdmin = defaults.Single(t => t.Tier == UserTier.SuperAdmin);
        Assert.Equal(-1, superAdmin.DailyAiDetectionLimit);
        Assert.True(superAdmin.AllowPhotoCompare);
        Assert.True(superAdmin.AllowDataExport);
    }
}

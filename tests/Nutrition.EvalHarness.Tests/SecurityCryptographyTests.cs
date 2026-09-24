using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Security;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class SecurityCryptographyTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly OtpService _otpService = new();

    [Fact]
    public void Pbkdf2PasswordHasher_HashPassword_ProducesCompliantHeaderAndSalt()
    {
        var password = "SecurePassword123!#";
        var hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.StartsWith("PBKDF2$SHA512$100000$", hash);

        var parts = hash.Split('$');
        Assert.Equal(5, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.Equal("SHA512", parts[1]);
        Assert.Equal("100000", parts[2]);
        Assert.NotEmpty(parts[3]); // Salt
        Assert.NotEmpty(parts[4]); // Subkey
    }

    [Fact]
    public void Pbkdf2PasswordHasher_VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var password = "MySecretClinicalPassword@2026";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(password, hash);
        Assert.True(isValid);
    }

    [Fact]
    public void Pbkdf2PasswordHasher_VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword("WrongPassword456!", hash);
        Assert.False(isValid);
    }

    [Fact]
    public void Pbkdf2PasswordHasher_HashPassword_GeneratesUniqueSalts()
    {
        var password = "IdenticalPassword!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword(password, hash1));
        Assert.True(_hasher.VerifyPassword(password, hash2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Pbkdf2PasswordHasher_EmptyPassword_ThrowsArgumentException(string emptyPassword)
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(emptyPassword));
    }

    [Fact]
    public void OtpService_GenerateCode_ReturnsSixDigitNumericString()
    {
        for (int i = 0; i < 50; i++)
        {
            var code = _otpService.GenerateCode();
            Assert.NotNull(code);
            Assert.Equal(6, code.Length);
            Assert.True(int.TryParse(code, out var num));
            Assert.InRange(num, 100_000, 999_999);
        }
    }

    [Fact]
    public void OtpService_CreateAndValidateCode_Succeeds()
    {
        var rawCode = _otpService.GenerateCode();
        var otp = _otpService.CreateOtp("user-123", "test@example.com", OtpChannel.Email, rawCode);

        Assert.NotNull(otp);
        Assert.Equal("user-123", otp.UserId);
        Assert.Equal("test@example.com", otp.Target);
        Assert.Equal(OtpChannel.Email, otp.Channel);
        Assert.False(otp.IsUsed);
        Assert.Equal(0, otp.AttemptCount);

        var isValid = _otpService.ValidateCode(otp, rawCode, out var failureReason);
        Assert.True(isValid);
        Assert.Null(failureReason);
        Assert.True(otp.IsUsed);
    }

    [Fact]
    public void OtpService_ValidateCode_WrongCode_FailsWithFeedback()
    {
        var rawCode = "123456";
        var otp = _otpService.CreateOtp("user-123", "test@example.com", OtpChannel.Email, rawCode);

        var isValid = _otpService.ValidateCode(otp, "654321", out var failureReason);
        Assert.False(isValid);
        Assert.NotNull(failureReason);
        Assert.Contains("attempt(s) remaining", failureReason);
    }
}

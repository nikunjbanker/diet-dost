/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;
using Xunit;

namespace Nutrition.Domain.Tests;

public class Bip39MnemonicTests
{
    [Fact]
    public void Generate_ProducesExact128BitEntropyAnd12Words()
    {
        var kit = RecoveryKit.GenerateNew("Test User");

        Assert.NotNull(kit);
        Assert.False(string.IsNullOrWhiteSpace(kit.MasterEntropyHex));
        Assert.Equal(32, kit.MasterEntropyHex.Length); // 16 bytes = 32 hex chars

        Assert.False(string.IsNullOrWhiteSpace(kit.MnemonicPhrase));
        var words = kit.MnemonicPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(12, words.Length);

        Assert.False(string.IsNullOrWhiteSpace(kit.RecoveryLookupId));
        Assert.Equal(24, kit.RecoveryLookupId.Length);

        Assert.False(string.IsNullOrWhiteSpace(kit.RecoveryKeyHash));
        Assert.False(string.IsNullOrWhiteSpace(kit.RecoverySalt));
    }

    [Fact]
    public void Checksum_Valid12WordsPass_AlteredWordFails()
    {
        var kit = RecoveryKit.GenerateNew("Aarav");
        Assert.True(Bip39Mnemonic.ValidateMnemonic(kit.MnemonicPhrase, out var entropy));
        Assert.Equal(kit.MasterEntropyHex, Convert.ToHexString(entropy).ToLowerInvariant());

        // Alter 1 word
        var words = kit.MnemonicPhrase.Split(' ');
        words[0] = words[0] == "abandon" ? "ability" : "abandon";
        var corrupted = string.Join(' ', words);

        // May fail either checksum or word check
        var isValid = Bip39Mnemonic.ValidateMnemonic(corrupted, out _);
        Assert.False(isValid, "Corrupted mnemonic must fail BIP-39 validation");
    }

    [Fact]
    public void UnicodeNfkd_NormalizesUppercaseAndAccentedSpaces()
    {
        var kit = RecoveryKit.GenerateNew("User");
        var noisy = "  " + kit.MnemonicPhrase.ToUpperInvariant().Replace(" ", "   ") + "  \n";

        Assert.True(Bip39Mnemonic.ValidateMnemonic(noisy, out var entropy));
        Assert.Equal(kit.MasterEntropyHex, Convert.ToHexString(entropy).ToLowerInvariant());
    }

    [Fact]
    public void HkdfLookupId_DerivationIsDeterministic()
    {
        var kit = RecoveryKit.GenerateNew("User");
        var derivedId = RecoveryKit.DeriveLookupId(Convert.FromHexString(kit.MasterEntropyHex));

        Assert.Equal(kit.RecoveryLookupId, derivedId);
    }

    [Fact]
    public void ValidateRegistration_AnonymousUserWithRecoveryHashAndConsent_Succeeds()
    {
        var kit = RecoveryKit.GenerateNew("Anonymous User");
        var user = new ApplicationUser
        {
            IsAnonymous = true,
            DisplayName = "Anonymous User",
            Email = "anon_12345@dietdost.local",
            NormalizedEmail = "ANON_12345@DIETDOST.LOCAL",
            MobileNumber = "ANONYMOUS",
            PasswordHash = "ANONYMOUS_NO_PASSWORD",
            RecoveryLookupId = kit.RecoveryLookupId,
            RecoveryKeyHash = kit.RecoveryKeyHash,
            RecoverySalt = kit.RecoverySalt,
            RecoveryKeyCreatedAtUtc = DateTime.UtcNow,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };

        // Invariant check
        user.ValidateRegistration();
        Assert.True(user.IsAnonymous);
        Assert.Equal("ANONYMOUS", user.MobileNumber);
    }

    [Fact]
    public void ValidateRegistration_AnonymousUserWithoutRecoveryHash_ThrowsInvalidOperationException()
    {
        var user = new ApplicationUser
        {
            IsAnonymous = true,
            DisplayName = "Anonymous User",
            Email = "anon_test@dietdost.local",
            NormalizedEmail = "ANON_TEST@DIETDOST.LOCAL",
            MobileNumber = "ANONYMOUS",
            PasswordHash = "ANONYMOUS_NO_PASSWORD",
            RecoveryLookupId = null,
            RecoveryKeyHash = null,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("Emergency Recovery Key", ex.Message);
    }

    [Fact]
    public void ValidateRegistration_AnonymousUserWithoutConsent_ThrowsDpdpaComplianceViolation()
    {
        var kit = RecoveryKit.GenerateNew("Anonymous User");
        var user = new ApplicationUser
        {
            IsAnonymous = true,
            Email = "anon_test2@dietdost.local",
            NormalizedEmail = "ANON_TEST2@DIETDOST.LOCAL",
            MobileNumber = "ANONYMOUS",
            PasswordHash = "ANONYMOUS_NO_PASSWORD",
            RecoveryLookupId = kit.RecoveryLookupId,
            RecoveryKeyHash = kit.RecoveryKeyHash,
            RecoverySalt = kit.RecoverySalt,
            TermsAcceptedAtUtc = null, // Missing consent!
            TermsVersionAccepted = null,
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => user.ValidateRegistration());
        Assert.Contains("DPDPA Compliance Violation", ex.Message);
    }
}

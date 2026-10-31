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

        // 1. Alter 12th word by flipping checksum bit (bits 3..0) while keeping entropy identical
        var words = kit.MnemonicPhrase.Split(' ');
        var lastWord = words[11];
        int lastIdx = Array.IndexOf(Bip39EnglishWordList.Words, lastWord);
        // XOR with 1 flips the last checksum bit, keeping top 7 entropy bits identical
        int corruptedLastIdx = lastIdx ^ 1;
        words[11] = Bip39EnglishWordList.Words[corruptedLastIdx];
        var corruptedChecksum = string.Join(' ', words);

        Assert.False(Bip39Mnemonic.ValidateMnemonic(corruptedChecksum, out _), "Corrupted checksum must fail BIP-39 validation");

        // 2. Invalid word not in dictionary must also fail
        words[0] = "xyznotaword";
        var invalidWordPhrase = string.Join(' ', words);
        Assert.False(Bip39Mnemonic.ValidateMnemonic(invalidWordPhrase, out _), "Invalid dictionary word must fail validation");
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

    [Fact]
    public void UserUploadedKit_DecodesToExactHexAndMnemonic()
    {
        var phrase = "suspect vintage animal morning require leader genre youth very home trust jump";
        var expectedHex = "dade842447fb70fd1847fbf2eda3a6bc";
        Assert.True(Bip39Mnemonic.ValidateMnemonic(phrase, out var entropy));
        Assert.Equal(expectedHex, Convert.ToHexString(entropy).ToLowerInvariant());

        var encodedPhrase = Bip39Mnemonic.Encode(entropy);
        Assert.Equal(phrase, encodedPhrase);

        var lookupId = RecoveryKit.DeriveLookupId(entropy);
        Assert.Equal("ff4dca8410f4187add5a0478", lookupId);

        var saltHex = "a1b2c3d4e5f67890123456789abcdef0";
        var salt = Convert.FromHexString(saltHex);
        var keyHash = RecoveryKit.ComputeKeyHash(entropy, salt);
        Assert.Equal("f22c3a7164a9f7d69ee72a2a9139d2cf3c9f25ca3b0dd02ee79dc58e6d956463bb5694b88732e28c3326b2213ae61391f319ee78120fd6139832c9adcbcd984e", keyHash);
        Assert.True(RecoveryKit.VerifyKey(entropy, salt, keyHash));
    }
}

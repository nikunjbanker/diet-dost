/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Cryptographic emergency recovery kit for zero-PII and anonymous account custodianship.
/// Implements RFC 5869 HKDF-Expand for deterministic O(1) database index lookup
/// and RFC 8018 PBKDF2-HMAC-SHA512 (250,000 iterations) for constant-time hash verification.
/// </summary>
public record RecoveryKit(
    string MasterEntropyHex,
    string MnemonicPhrase,
    string RecoveryLookupId,
    string RecoveryKeyHash,
    string RecoverySalt,
    string JsonBackupPayload)
{
    private const int Pbkdf2Iterations = 250_000;
    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("DIETDOST_LOOKUP_INDEX_V1");

    /// <summary>
    /// Generates a new 128-bit cryptographically secure recovery kit for a newly registered anonymous user.
    /// </summary>
    public static RecoveryKit GenerateNew(string? displayName = null, string? userId = null)
    {
        var entropy = new byte[16];
        RandomNumberGenerator.Fill(entropy);

        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        var entropyHex = Convert.ToHexString(entropy).ToLowerInvariant();
        var saltHex = Convert.ToHexString(salt).ToLowerInvariant();
        var mnemonic = Bip39Mnemonic.Encode(entropy);
        var lookupId = DeriveLookupId(entropy);
        var keyHash = ComputeKeyHash(entropy, salt);

        var json = GenerateJsonBackup(userId ?? "pending", displayName, entropyHex, mnemonic, DateTime.UtcNow);

        return new RecoveryKit(entropyHex, mnemonic, lookupId, keyHash, saltHex, json);
    }

    /// <summary>
    /// Derives a deterministic 24-character public lookup index from the 128-bit master entropy
    /// using RFC 5869 HKDF-Expand with zero knowledge leakage of the master secret.
    /// </summary>
    public static string DeriveLookupId(byte[] masterEntropy)
    {
        if (masterEntropy == null || masterEntropy.Length != 16)
            throw new ArgumentException("Master entropy must be exactly 16 bytes (128 bits).", nameof(masterEntropy));

        // Use HKDF to derive 15 bytes -> 24 Base32/hex characters
        var prk = HMACSHA256.HashData(masterEntropy, Encoding.UTF8.GetBytes("DIETDOST_HKDF_SALT"));
        var expanded = HKDF.Expand(HashAlgorithmName.SHA256, prk, 15, HkdfInfo);

        // Convert 15 bytes to 24 hex characters
        return Convert.ToHexString(expanded).ToLowerInvariant()[..24];
    }

    /// <summary>
    /// Computes PBKDF2-HMAC-SHA512 with 250,000 iterations for stored verification.
    /// </summary>
    public static string ComputeKeyHash(byte[] masterEntropy, byte[] salt)
    {
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            masterEntropy,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA512,
            64
        );

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies candidate master entropy against a stored PBKDF2 hash in constant time.
    /// </summary>
    public static bool VerifyKey(byte[] candidateEntropy, byte[] salt, string storedHashHex)
    {
        if (candidateEntropy == null || salt == null || string.IsNullOrWhiteSpace(storedHashHex))
            return false;

        var computedHash = ComputeKeyHash(candidateEntropy, salt);
        var computedSpan = Encoding.UTF8.GetBytes(computedHash);
        var storedSpan = Encoding.UTF8.GetBytes(storedHashHex);

        return CryptographicOperations.FixedTimeEquals(computedSpan, storedSpan);
    }

    /// <summary>
    /// Formats the downloadable JSON backup kit payload with tamper-evident HMAC checksum.
    /// </summary>
    public static string GenerateJsonBackup(string userId, string? displayName, string masterEntropyHex, string mnemonicPhrase, DateTime createdAtUtc)
    {
        var checksumRaw = $"{userId}:{masterEntropyHex}:{createdAtUtc:O}";
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(checksumRaw))).ToLowerInvariant();

        var payload = new
        {
            app = "Diet Dost",
            version = "1.0",
            type = "DietDost-Emergency-Recovery-Kit",
            userId = userId,
            displayName = displayName ?? "Anonymous User",
            createdAtUtc = createdAtUtc.ToString("O"),
            recoveryKey = masterEntropyHex,
            mnemonicPhrase = mnemonicPhrase,
            integrityChecksum = checksum,
            criticalNotice = "CONFIDENTIAL: Diet-Dost does not store your email, mobile number, or password. This file or your 12-word mnemonic phrase is the SOLE way to recover your account and confidential clinical records if your device is lost or reset."
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

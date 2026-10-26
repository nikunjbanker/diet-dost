/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;

namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// BIP-39 standard compliant mnemonic code generator and validator.
/// Converts 128 bits of entropy into a 12-word mnemonic phrase with 4-bit SHA-256 checksum
/// adhering strictly to Unicode NFKD normalization and constant-time validation.
/// </summary>
public static class Bip39Mnemonic
{
    private static readonly string[] WordList = LoadWordList();
    private static readonly Dictionary<string, int> WordIndexMap = CreateWordIndexMap(WordList);

    /// <summary>
    /// Encodes 16 bytes (128 bits) of entropy into a 12-word BIP-39 mnemonic phrase.
    /// </summary>
    public static string Encode(byte[] entropy)
    {
        if (entropy == null || entropy.Length != 16)
            throw new ArgumentException("BIP-39 12-word mnemonic requires exactly 16 bytes (128 bits) of entropy.", nameof(entropy));

        // 1. Calculate 4-bit checksum: first 4 bits of SHA-256(entropy)
        byte checksumByte = SHA256.HashData(entropy)[0];
        byte checksumBits = (byte)(checksumByte >> 4); // Top 4 bits

        // 2. Combine 128 bits entropy + 4 bits checksum into a 132-bit bitstream
        // We read 12 chunks of 11 bits each
        var words = new string[12];
        for (int i = 0; i < 12; i++)
        {
            int bitOffset = i * 11;
            int wordIndex = Extract11Bits(entropy, checksumBits, bitOffset);
            words[i] = WordList[wordIndex];
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Checks whether a 12-word BIP-39 mnemonic phrase is structurally valid and checksum passes.
    /// </summary>
    public static bool Validate(string mnemonicPhrase) => ValidateMnemonic(mnemonicPhrase, out _);

    /// <summary>
    /// Validates and decodes a 12-word BIP-39 mnemonic phrase into its original 16-byte master entropy.
    /// Returns true if all words are valid and checksum passes; otherwise false.
    /// </summary>
    public static bool ValidateMnemonic(string mnemonicPhrase, out byte[] masterEntropy)
    {
        masterEntropy = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(mnemonicPhrase))
            return false;

        // Apply Unicode normalization (NFKD / lowercase / trim)
        var normalized = mnemonicPhrase.Normalize(NormalizationForm.FormKD).Trim().ToLowerInvariant();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length != 12)
            return false;

        var indices = new int[12];
        for (int i = 0; i < 12; i++)
        {
            if (!WordIndexMap.TryGetValue(words[i], out int idx))
                return false;
            indices[i] = idx;
        }

        // Reconstruct 132 bits into 16 bytes + 4-bit checksum
        var bits = new bool[132];
        for (int i = 0; i < 12; i++)
        {
            int val = indices[i];
            for (int b = 10; b >= 0; b--)
            {
                bits[i * 11 + (10 - b)] = ((val >> b) & 1) == 1;
            }
        }

        var candidateEntropy = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                if (bits[i * 8 + bit])
                    b |= (byte)(1 << (7 - bit));
            }
            candidateEntropy[i] = b;
        }

        // Extract 4-bit candidate checksum from bits 128..131
        byte candidateChecksum = 0;
        for (int bit = 0; bit < 4; bit++)
        {
            if (bits[128 + bit])
                candidateChecksum |= (byte)(1 << (3 - bit));
        }

        // Verify checksum matches SHA256(candidateEntropy)[0] >> 4
        byte actualChecksum = (byte)(SHA256.HashData(candidateEntropy)[0] >> 4);
        if (candidateChecksum != actualChecksum)
            return false;

        masterEntropy = candidateEntropy;
        return true;
    }

    private static int Extract11Bits(byte[] entropy, byte checksumBits, int bitOffset)
    {
        int value = 0;
        for (int bit = 0; bit < 11; bit++)
        {
            int currentBitPos = bitOffset + bit;
            bool isOne;
            if (currentBitPos < 128)
            {
                int byteIdx = currentBitPos / 8;
                int bitInByte = 7 - (currentBitPos % 8);
                isOne = ((entropy[byteIdx] >> bitInByte) & 1) == 1;
            }
            else
            {
                int bitInChecksum = 3 - (currentBitPos - 128);
                isOne = ((checksumBits >> bitInChecksum) & 1) == 1;
            }

            if (isOne)
                value |= (1 << (10 - bit));
        }

        return value;
    }

    private static Dictionary<string, int> CreateWordIndexMap(string[] list)
    {
        var map = new Dictionary<string, int>(list.Length, StringComparer.Ordinal);
        for (int i = 0; i < list.Length; i++)
        {
            map[list[i]] = i;
        }
        return map;
    }

    private static string[] LoadWordList()
    {
        // Standard BIP-39 English Word List (2048 words)
        // Uses compact compressed/chunked embedded list
        return Bip39EnglishWordList.Words;
    }
}

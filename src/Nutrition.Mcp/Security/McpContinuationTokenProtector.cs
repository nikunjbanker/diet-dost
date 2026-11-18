/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Nutrition.Mcp.Models;

namespace Nutrition.Mcp.Security;

public interface IMcpContinuationTokenProtector
{
    string Protect(DraftMealContinuationState state);
    DraftMealContinuationState? Unprotect(string token, string expectedUserId);
}

/// <summary>
/// Cryptographic protector for MCP Human-in-the-Loop continuation tokens.
/// Enforces user-bound purpose strings, 15-minute expiration TTL, and tamper resistance.
/// </summary>
public class McpContinuationTokenProtector : IMcpContinuationTokenProtector
{
    private const string BasePurpose = "DietDost.Mcp.Elicitation.v2";
    private readonly IDataProtectionProvider _dataProtection;

    public McpContinuationTokenProtector(IDataProtectionProvider dataProtection)
    {
        _dataProtection = dataProtection;
    }

    public string Protect(DraftMealContinuationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.UserId))
            throw new ArgumentException("State must contain a valid UserId for cryptographic binding.", nameof(state));

        var protector = _dataProtection.CreateProtector(BasePurpose, state.UserId);
        var stateBytes = JsonSerializer.SerializeToUtf8Bytes(state);
        return Convert.ToBase64String(protector.Protect(stateBytes));
    }

    public DraftMealContinuationState? Unprotect(string token, string expectedUserId)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedUserId))
            return null;

        try
        {
            var protector = _dataProtection.CreateProtector(BasePurpose, expectedUserId);
            var cipherBytes = Convert.FromBase64String(token);
            var decryptedBytes = protector.Unprotect(cipherBytes);
            var state = JsonSerializer.Deserialize<DraftMealContinuationState>(decryptedBytes);

            if (state == null)
                return null;

            // Enforce user binding
            if (!string.Equals(state.UserId, expectedUserId, StringComparison.Ordinal))
                return null;

            // Enforce 15-minute TTL
            if (DateTime.UtcNow > state.ExpiresAtUtc)
                return null;

            return state;
        }
        catch
        {
            // Decryption failure or cryptographic tampering
            return null;
        }
    }
}

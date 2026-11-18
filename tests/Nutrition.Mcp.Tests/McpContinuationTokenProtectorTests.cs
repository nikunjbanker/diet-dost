/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Mcp.Models;
using Nutrition.Mcp.Security;
using Xunit;

namespace Nutrition.Mcp.Tests;

public class McpContinuationTokenProtectorTests
{
    private readonly IMcpContinuationTokenProtector _protector;

    public McpContinuationTokenProtectorTests()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddScoped<IMcpContinuationTokenProtector, McpContinuationTokenProtector>();
        var sp = services.BuildServiceProvider();
        _protector = sp.GetRequiredService<IMcpContinuationTokenProtector>();
    }

    [Fact]
    public void ProtectAndUnprotect_ValidState_Succeeds()
    {
        var state = new DraftMealContinuationState(
            UserId: "user123@dietdost.app",
            MealType: "Lunch",
            Description: "2 rotis and dal",
            TotalCalories: 350,
            TotalProtein: 12,
            TotalCarbs: 55,
            TotalFat: 8,
            TotalFiber: 6,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            Nonce: Guid.NewGuid().ToString("N"),
            Items: new List<DraftItem>
            {
                new("Roti", "2 pieces", 240, 6, 44, 4, 4),
                new("Dal Tadka", "1 bowl", 110, 6, 11, 4, 2)
            }
        );

        var token = _protector.Protect(state);
        Assert.NotNull(token);

        var unpacked = _protector.Unprotect(token, "user123@dietdost.app");
        Assert.NotNull(unpacked);
        Assert.Equal("user123@dietdost.app", unpacked.UserId);
        Assert.Equal("Lunch", unpacked.MealType);
        Assert.Equal(350, unpacked.TotalCalories);
        Assert.Equal(2, unpacked.Items.Count);
    }

    [Fact]
    public void Unprotect_CrossUserTokenReplay_ReturnsNull()
    {
        var state = new DraftMealContinuationState(
            UserId: "victim@dietdost.app",
            MealType: "Lunch",
            Description: "Salad",
            TotalCalories: 150,
            TotalProtein: 4,
            TotalCarbs: 20,
            TotalFat: 3,
            TotalFiber: 5,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            Nonce: Guid.NewGuid().ToString("N"),
            Items: new List<DraftItem>()
        );

        var token = _protector.Protect(state);

        // Attacker attempts to redeem victim's token
        var result = _protector.Unprotect(token, "attacker@dietdost.app");
        Assert.Null(result);
    }

    [Fact]
    public void Unprotect_ExpiredToken_ReturnsNull()
    {
        var expiredState = new DraftMealContinuationState(
            UserId: "user123@dietdost.app",
            MealType: "Dinner",
            Description: "Dinner",
            TotalCalories: 400,
            TotalProtein: 20,
            TotalCarbs: 50,
            TotalFat: 10,
            TotalFiber: 5,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            Nonce: Guid.NewGuid().ToString("N"),
            Items: new List<DraftItem>()
        );

        var token = _protector.Protect(expiredState);
        var result = _protector.Unprotect(token, "user123@dietdost.app");
        Assert.Null(result);
    }

    [Fact]
    public void Unprotect_TamperedToken_ReturnsNull()
    {
        var result = _protector.Unprotect("invalid-base64-random-ciphertext", "user123@dietdost.app");
        Assert.Null(result);
    }
}

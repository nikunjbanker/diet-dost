/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Common.Options.Validators;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class OptionsPatternFluentValidationTests
{
    [Fact]
    public void JwtOptionsValidator_ValidOptions_PassesValidation()
    {
        var validator = new JwtOptionsValidator();
        var options = new JwtOptions
        {
            Issuer = "DietDostGateway",
            Audience = "DietDostClient",
            Key = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!",
            ExpiryMinutes = 1440
        };

        var result = validator.Validate(options);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void JwtOptionsValidator_InvalidKeyOrShortKey_FailsValidation()
    {
        var validator = new JwtOptionsValidator();
        var options = new JwtOptions
        {
            Issuer = "",
            Audience = "",
            Key = "TooShort",
            ExpiryMinutes = -5
        };

        var result = validator.Validate(options);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(JwtOptions.Issuer));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(JwtOptions.Audience));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(JwtOptions.Key));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(JwtOptions.ExpiryMinutes));
    }

    [Fact]
    public void AuthOptionsValidator_ValidOptions_PassesValidation()
    {
        var validator = new AuthOptionsValidator();
        var options = new AuthOptions
        {
            SuperAdminEmail = "admin@dietdost.app",
            TermsVersion = "v1.0-202609",
            HealthConsentVersion = "v1.0-202609",
            AllowRegistration = true,
            RequireMobileVerification = false
        };

        var result = validator.Validate(options);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AuthOptionsValidator_InvalidEmail_FailsValidation()
    {
        var validator = new AuthOptionsValidator();
        var options = new AuthOptions
        {
            SuperAdminEmail = "not-an-email",
            TermsVersion = "",
            HealthConsentVersion = ""
        };

        var result = validator.Validate(options);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AuthOptions.SuperAdminEmail));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AuthOptions.TermsVersion));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AuthOptions.HealthConsentVersion));
    }

    [Fact]
    public void AiOptionsValidator_ValidOptions_PassesValidation()
    {
        var validator = new AiOptionsValidator();
        var options = new AiOptions
        {
            Provider = "GoogleAI",
            GoogleAI = new GoogleAiModelOptions
            {
                ModelId = "gemini-3-flash-preview"
            },
            MaxTokens = 8192,
            Temperature = 0.2
        };

        var result = validator.Validate(options);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AiOptionsValidator_InvalidProviderOrTokens_FailsValidation()
    {
        var validator = new AiOptionsValidator();
        var options = new AiOptions
        {
            Provider = "UnsupportedProvider",
            MaxTokens = -10,
            Temperature = 5.0
        };

        var result = validator.Validate(options);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AiOptions.Provider));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AiOptions.MaxTokens));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AiOptions.Temperature));
    }

    [Fact]
    public void DatabaseOptionsValidator_ValidOptions_PassesValidation()
    {
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Provider = "Sqlite",
            ConnectionString = "Data Source=diettracker.db"
        };

        var result = validator.Validate(options);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void DatabaseOptionsValidator_EmptyFields_FailsValidation()
    {
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Provider = "",
            ConnectionString = ""
        };

        var result = validator.Validate(options);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DatabaseOptions.Provider));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DatabaseOptions.ConnectionString));
    }

    [Fact]
    public void AddDietDostOptions_RegistersAndResolves_TypedOptions_Via_IOptions()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!",
            ["Jwt:Issuer"] = "DietDostGateway",
            ["Jwt:Audience"] = "DietDostClient",
            ["Jwt:ExpiryMinutes"] = "1440",
            ["Auth:SuperAdminEmail"] = "superadmin@dietdost.app",
            ["Auth:AllowRegistration"] = "true",
            ["Auth:RequireMobileVerification"] = "false",
            ["Auth:TermsVersion"] = "v1.0-202609",
            ["Auth:HealthConsentVersion"] = "v1.0-202609",
            ["AI:Provider"] = "GoogleAI",
            ["AI:GoogleAI:ModelId"] = "gemini-3-flash-preview",
            ["AI:MaxTokens"] = "8192",
            ["AI:Temperature"] = "0.2",
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:DefaultConnection"] = "Data Source=diettracker.db"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddDietDostOptions(configuration);
        var provider = services.BuildServiceProvider();

        var jwtOptions = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var authOptions = provider.GetRequiredService<IOptions<AuthOptions>>().Value;
        var aiOptions = provider.GetRequiredService<IOptions<AiOptions>>().Value;
        var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var storageOptions = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

        Assert.Equal("DietDostGateway", jwtOptions.Issuer);
        Assert.Equal("superadmin@dietdost.app", authOptions.SuperAdminEmail);
        Assert.Equal("GoogleAI", aiOptions.Provider);
        Assert.Equal("Sqlite", dbOptions.Provider);
        Assert.Equal("Data Source=diettracker.db", dbOptions.ConnectionString);
        Assert.NotNull(storageOptions);
    }

    [Fact]
    public void StorageOptionsValidator_ValidOptions_PassesValidation()
    {
        var validator = new StorageOptionsValidator();
        var options = new StorageOptions { WebRootPath = "/app/wwwroot" };
        var result = validator.Validate(options);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AddDietDostOptions_WhenConfigurationInvalid_ThrowsOptionsValidationException()
    {
        var invalidConfigData = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "short", // Invalid: < 32 bytes
            ["Jwt:Issuer"] = "",
            ["Auth:SuperAdminEmail"] = "invalid-email"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(invalidConfigData)
            .Build();

        var services = new ServiceCollection();
        services.AddDietDostOptions(configuration);
        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        });

        Assert.Contains("Options validation failed", ex.Message);
    }
}

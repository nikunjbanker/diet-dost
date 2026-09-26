/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Extensions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class JwtAuthenticationTests
{
    private readonly IConfiguration _config;
    private readonly JwtTokenService _jwtService;

    public JwtAuthenticationTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "DietDostTestIssuer",
            ["Jwt:Audience"] = "DietDostTestAudience",
            ["Jwt:Key"] = "DietDost_Super_Secret_Test_Key_For_Jwt_HMAC_SHA256_Authentication_2026_Min32Bytes!",
            ["Jwt:ExpiryMinutes"] = "60"
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtService = new JwtTokenService(_config);
    }

    [Fact]
    public void GenerateToken_ProducesValidHmacSha256SignedJwt()
    {
        var user = new ApplicationUser
        {
            Id = "user-jwt-001",
            Email = "jwt.tester@dietdost.app",
            NormalizedEmail = "JWT.TESTER@DIETDOST.APP",
            MobileNumber = "+919876543210",
            NormalizedMobileNumber = "+919876543210",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.User,
            Tier = UserTier.Basic,
            IsEmailVerified = true,
            IsMobileVerified = true,
            IsActive = true
        };

        var tokenString = _jwtService.GenerateToken(user, "JWT Tester");

        Assert.NotNull(tokenString);
        var parts = tokenString.Split('.');
        Assert.Equal(3, parts.Length); // header, payload, signature

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.Equal("HS256", jwtToken.Header.Alg);
        Assert.Equal("DietDostTestIssuer", jwtToken.Issuer);
        Assert.Contains("DietDostTestAudience", jwtToken.Audiences);
    }

    [Fact]
    public void GenerateToken_EmbedsRequiredIdentityAndTierClaims()
    {
        var user = new ApplicationUser
        {
            Id = "user-jwt-admin-002",
            Email = "admin.jwt@dietdost.app",
            NormalizedEmail = "ADMIN.JWT@DIETDOST.APP",
            MobileNumber = "+919999988888",
            NormalizedMobileNumber = "+919999988888",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.Admin,
            Tier = UserTier.Premium,
            IsEmailVerified = true,
            IsMobileVerified = false,
            IsActive = true
        };

        var tokenString = _jwtService.GenerateToken(user, "Admin Commander");
        var principal = _jwtService.ValidateToken(tokenString);

        Assert.NotNull(principal);
        Assert.True(principal.Identity?.IsAuthenticated);

        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal("user-jwt-admin-002", sub);

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        Assert.Equal("admin.jwt@dietdost.app", email);

        var name = principal.FindFirst(ClaimTypes.Name)?.Value;
        Assert.Equal("Admin Commander", name);

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        Assert.Equal(nameof(UserRole.Admin), role);
        Assert.True(principal.IsInRole(nameof(UserRole.Admin)));

        var tier = principal.FindFirst("tier")?.Value;
        Assert.Equal(nameof(UserTier.Premium), tier);

        var isEmailVerified = principal.FindFirst("isEmailVerified")?.Value;
        Assert.Equal("true", isEmailVerified);

        var isMobileVerified = principal.FindFirst("isMobileVerified")?.Value;
        Assert.Equal("false", isMobileVerified);
    }

    [Fact]
    public void ValidateToken_TamperedSignature_ReturnsNull()
    {
        var user = new ApplicationUser
        {
            Id = "user-jwt-tamper-003",
            Email = "tamper@dietdost.app",
            NormalizedEmail = "TAMPER@DIETDOST.APP",
            MobileNumber = "+919123456789",
            NormalizedMobileNumber = "+919123456789",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true,
            IsMobileVerified = false,
            IsActive = true
        };

        var validToken = _jwtService.GenerateToken(user, "Tamper Target");
        var parts = validToken.Split('.');

        // Tamper with the signature portion
        var tamperedSignature = parts[2].Substring(0, parts[2].Length - 4) + "XXXX";
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        var principal = _jwtService.ValidateToken(tamperedToken);
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsNull()
    {
        var expiredConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "DietDostTestIssuer",
                ["Jwt:Audience"] = "DietDostTestAudience",
                ["Jwt:Key"] = "DietDost_Super_Secret_Test_Key_For_Jwt_HMAC_SHA256_Authentication_2026_Min32Bytes!",
                ["Jwt:ExpiryMinutes"] = "-5" // Already expired 5 minutes ago
            })
            .Build();

        var expiredService = new JwtTokenService(expiredConfig);

        var user = new ApplicationUser
        {
            Id = "user-jwt-expired-004",
            Email = "expired@dietdost.app",
            NormalizedEmail = "EXPIRED@DIETDOST.APP",
            MobileNumber = "+919000011111",
            NormalizedMobileNumber = "+919000011111",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true,
            IsMobileVerified = false,
            IsActive = true
        };

        var expiredToken = expiredService.GenerateToken(user);
        var principal = _jwtService.ValidateToken(expiredToken);

        Assert.Null(principal);
    }

    [Fact]
    public void RoleBasedAuthorization_DistinguishesSuperAdminFromRegularUser()
    {
        var superAdmin = new ApplicationUser
        {
            Id = "user-super-005",
            Email = "super@dietdost.app",
            NormalizedEmail = "SUPER@DIETDOST.APP",
            MobileNumber = "+919888877777",
            NormalizedMobileNumber = "+919888877777",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin,
            IsEmailVerified = true,
            IsMobileVerified = true,
            IsActive = true
        };

        var regularUser = new ApplicationUser
        {
            Id = "user-regular-006",
            Email = "regular@dietdost.app",
            NormalizedEmail = "REGULAR@DIETDOST.APP",
            MobileNumber = "+919777766666",
            NormalizedMobileNumber = "+919777766666",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true,
            IsMobileVerified = false,
            IsActive = true
        };

        var superToken = _jwtService.GenerateToken(superAdmin, "Super Admin");
        var userToken = _jwtService.GenerateToken(regularUser, "Regular User");

        var superPrincipal = _jwtService.ValidateToken(superToken);
        var userPrincipal = _jwtService.ValidateToken(userToken);

        Assert.NotNull(superPrincipal);
        Assert.NotNull(userPrincipal);

        Assert.True(superPrincipal.IsInRole(nameof(UserRole.SuperAdmin)));
        Assert.False(superPrincipal.IsInRole(nameof(UserRole.User)));

        Assert.True(userPrincipal.IsInRole(nameof(UserRole.User)));
        Assert.False(userPrincipal.IsInRole(nameof(UserRole.Admin)));
        Assert.False(userPrincipal.IsInRole(nameof(UserRole.SuperAdmin)));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567890123456789012345678901")] // 31 bytes
    public void JwtTokenService_KeyShorterThan32Bytes_ThrowsArgumentException(string shortKey)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "DietDostTestIssuer",
            ["Jwt:Audience"] = "DietDostTestAudience",
            ["Jwt:Key"] = shortKey
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var ex = Assert.Throws<ArgumentException>(() => new JwtTokenService(config));
        Assert.Contains("32 bytes", ex.Message);
    }

    [Fact]
    public void UserClaimsExtensions_ShouldMapBothStandardAndShortJwtClaimTypes()
    {
        // 1. Standard SOAP/URI claim types (emitted by ASP.NET Cookie auth)
        var standardIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-standard-001"),
            new Claim(ClaimTypes.Email, "standard@dietdost.app"),
            new Claim(ClaimTypes.Role, nameof(UserRole.Admin)),
            new Claim("tier", nameof(UserTier.Premium))
        }, "Cookies");

        var standardPrincipal = new ClaimsPrincipal(standardIdentity);

        Assert.Equal("user-standard-001", standardPrincipal.GetUserId());
        Assert.Equal("standard@dietdost.app", standardPrincipal.GetEmail());
        Assert.Equal(nameof(UserRole.Admin), standardPrincipal.GetRole());
        Assert.Equal(nameof(UserTier.Premium), standardPrincipal.GetTier());
        Assert.True(standardPrincipal.IsAdminOrSuper());

        // 2. Short JWT claim types (RFC 7519 / OIDC standards emitted in bearer tokens)
        var jwtIdentity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "user-jwt-short-002"),
            new Claim("email", "jwt.short@dietdost.app"),
            new Claim("role", nameof(UserRole.User)),
            new Claim("tier", nameof(UserTier.Basic))
        }, "Bearer");

        var jwtPrincipal = new ClaimsPrincipal(jwtIdentity);

        Assert.Equal("user-jwt-short-002", jwtPrincipal.GetUserId());
        Assert.Equal("jwt.short@dietdost.app", jwtPrincipal.GetEmail());
        Assert.Equal(nameof(UserRole.User), jwtPrincipal.GetRole());
        Assert.Equal(nameof(UserTier.Basic), jwtPrincipal.GetTier());
        Assert.False(jwtPrincipal.IsAdminOrSuper());
    }

    [Fact]
    public void UserClaimsExtensions_IsAdminOrSuper_ValidatesRolesCorrectly()
    {
        var superPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, nameof(UserRole.SuperAdmin))
        }));

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("role", nameof(UserRole.Admin))
        }));

        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, nameof(UserRole.User))
        }));

        var emptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.True(superPrincipal.IsAdminOrSuper());
        Assert.True(adminPrincipal.IsAdminOrSuper());
        Assert.False(userPrincipal.IsAdminOrSuper());
        Assert.False(emptyPrincipal.IsAdminOrSuper());
    }
}

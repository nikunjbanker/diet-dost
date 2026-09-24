using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Infrastructure.Security;

/// <summary>
/// Production-grade JWT token generator and validator adhering to OWASP ASVS guidelines.
/// Generates HMAC-SHA256 signed tokens embedded with identity, role, and tier claims.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    private readonly int _expiryMinutes;
    private readonly SymmetricSecurityKey _signingKey;

    public const string DefaultDevKey = "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!";

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "DietDostGateway";
        _audience = configuration["Jwt:Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "DietDostClient";
        _key = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? DefaultDevKey;

        if (int.TryParse(configuration["Jwt:ExpiryMinutes"] ?? Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES"), out var exp) && exp != 0)
        {
            _expiryMinutes = exp;
        }
        else
        {
            _expiryMinutes = 1440; // Default: 24 hours
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
    }

    /// <summary>
    /// Generates a signed JWT bearer token for the specified application user.
    /// </summary>
    public string GenerateToken(ApplicationUser user, string? displayName = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, !string.IsNullOrWhiteSpace(displayName) ? displayName : user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString()),
            new("tier", user.Tier.ToString()),
            new("isEmailVerified", user.IsEmailVerified.ToString().ToLowerInvariant()),
            new("isMobileVerified", user.IsMobileVerified.ToString().ToLowerInvariant())
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var issuedAt = _expiryMinutes < 0 ? now.AddMinutes(_expiryMinutes - 5) : now;
        var notBefore = issuedAt;
        var expires = now.AddMinutes(_expiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = issuedAt,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Validates the raw JWT string against signature, issuer, audience, and expiration.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is JwtSecurityToken jwtSecurityToken &&
                jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return principal;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

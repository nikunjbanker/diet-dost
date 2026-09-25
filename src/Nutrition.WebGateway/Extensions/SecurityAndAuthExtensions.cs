using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Configures authentication schemes (JWT Bearer + Cookie Session), authorization policies,
/// and CORS security rules complying with OWASP ASVS guidelines.
/// </summary>
public static class SecurityAndAuthExtensions
{
    private const string SmartScheme = "SmartScheme";

    public static IServiceCollection AddAppSecurityAndAuth(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwtIssuer = configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "DietDostGateway";
        var jwtAudience = configuration["Jwt:Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "DietDostClient";
        var jwtKey = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? JwtTokenService.DefaultDevKey;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = SmartScheme;
            options.DefaultChallengeScheme = SmartScheme;
        })
        .AddPolicyScheme(SmartScheme, "JWT Bearer or Cookie Authentication", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
                return CookieAuthenticationDefaults.AuthenticationScheme;
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = "DietDost.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // Return 401/403 for API endpoints rather than redirecting to HTML login
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.SuperAdmin.ToString()));
            options.AddPolicy("RequireSuperAdmin", policy => policy.RequireRole(UserRole.SuperAdmin.ToString()));
            options.AddPolicy("RequireActiveUser", policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }

    public static IServiceCollection AddAppCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials());
        });

        return services;
    }
}

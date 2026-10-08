/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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

    public static IServiceCollection AddAppSecurityAndAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? env = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var isDev = env?.IsDevelopment() ?? string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "DietDostGateway";
        var jwtAudience = configuration["Jwt:Audience"] ?? "DietDostClient";
        var jwtKey = configuration["Jwt:Key"]
            ?? (isDev ? ConfigurationExtensions.DefaultDevJwtKey : null)
            ?? throw new InvalidOperationException(
                "CRITICAL SECURITY CONFIGURATION ERROR: 'Jwt:Key' is not configured. " +
                "In non-development / deployed environments, the cryptographic JWT signing key MUST be provided via Azure Key Vault or secure environment variables.");

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

    public static IServiceCollection AddAppCors(this IServiceCollection services, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(env);

        services.AddCors(options =>
        {
            if (env.IsDevelopment())
            {
                options.AddPolicy("AppCorsPolicy", policy =>
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials());
            }
            else
            {
                // Strict production CORS policy restricted to dev.diet-dost.in and diet-dost.in
                options.AddPolicy("AppCorsPolicy", policy =>
                    policy.WithOrigins(
                              "https://dev.diet-dost.in",
                              "https://diet-dost.in",
                              "http://diet-dost.in"
                          )
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials());
            }
        });

        return services;
    }
}

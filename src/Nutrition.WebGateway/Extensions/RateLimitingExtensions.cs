/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Net;
using System.Threading.RateLimiting;
using Polly;
using Polly.RateLimiting;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Configures per-IP partitioned rate limiting (OWASP A04) and defense-in-depth middleware
/// to protect authentication and sensitive endpoints from brute-force attacks.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        var isDevelopment = environment.IsDevelopment();

        // Per-IP Partitioned Rate Limiter for Brute-Force Defense (OWASP A04)
        // In development/loopback mode, higher limits prevent locking developers/testers while switching demo accounts.
        var authPartitionedRateLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ip = context.Connection.RemoteIpAddress;
            var isLoopback = ip != null && IPAddress.IsLoopback(ip);
            var permitLimit = (isDevelopment || isLoopback) ? 100 : 10;
            var window = (isDevelopment || isLoopback) ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15);

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: ip?.ToString()
                              ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                              ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    SegmentsPerWindow = 3,
                    QueueLimit = 0
                });
        });

        // Register under PartitionedRateLimiter<HttpContext> abstraction for DI resolution
        services.AddSingleton<PartitionedRateLimiter<HttpContext>>(authPartitionedRateLimiter);

        // Keep ResiliencePipeline registered for PollyRateLimitingTests and legacy consumers
        var legacyPollyPipeline = new ResiliencePipelineBuilder()
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = isDevelopment ? 200 : 50,
                Window = isDevelopment ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3,
                QueueLimit = 0
            }))
            .Build();
        services.AddSingleton(legacyPollyPipeline);

        return services;
    }

    /// <summary>
    /// Enforces per-IP rate limits on sensitive authentication endpoints (/api/auth/*).
    /// </summary>
    public static IApplicationBuilder UseAppRateLimiter(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/auth/login") ||
                context.Request.Path.StartsWithSegments("/api/auth/register") ||
                context.Request.Path.StartsWithSegments("/api/auth/verify-otp") ||
                context.Request.Path.StartsWithSegments("/api/auth/resend-otp") ||
                context.Request.Path.StartsWithSegments("/api/auth/token"))
            {
                var rateLimiter = context.RequestServices.GetRequiredService<PartitionedRateLimiter<HttpContext>>();
                using var lease = await rateLimiter.AcquireAsync(context, permitCount: 1);
                if (!lease.IsAcquired)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"error\":\"TooManyRequests\",\"message\":\"Too many authentication attempts from your IP. Please wait before retrying.\"}");
                    return;
                }
            }

            await next();
        });

        return app;
    }
}

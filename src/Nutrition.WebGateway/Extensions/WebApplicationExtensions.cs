/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.WebGateway.Middleware;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Configures the ASP.NET Core HTTP request processing pipeline in order of execution.
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication UseAppMiddlewarePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Capture request/response payloads in OpenTelemetry activity for observability
        app.UseMiddleware<HttpPayloadTelemetryMiddleware>();

        // Security, Static Assets & Identity
        app.UseCors("AllowAll");
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        // Enforce Per-IP Partitioned Rate Limiting on authentication endpoints (OWASP A04)
        app.UseAppRateLimiter();

        // Controller Endpoints & SPA Fallback
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        return app;
    }
}

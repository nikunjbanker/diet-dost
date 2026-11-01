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

        // 1. Enforce HTTPS & HSTS (Transport Security)
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        // 2. OWASP Top 10 Security Response Headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // 3. Deployed Environment Host Gating (dev.diet-dost.in & diet-dost.in)
        app.UseMiddleware<HostGatingMiddleware>();

        // 4. Security, Static Assets & Identity
        app.UseCors("AppCorsPolicy");
        app.UseResponseCompression();
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value ?? string.Empty;
                if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
                }
            }
        });

        // Persistent Azure File Share SMB Mount Static Media Serving (/uploads)
        var storageOptions = app.Services.GetService<Microsoft.Extensions.Options.IOptions<Nutrition.Application.Common.Options.StorageOptions>>()?.Value;
        var webRoot = !string.IsNullOrWhiteSpace(app.Environment.WebRootPath)
            ? app.Environment.WebRootPath
            : Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var persistentUploadsDir = !string.IsNullOrWhiteSpace(storageOptions?.WebRootPath)
            ? Path.Combine(storageOptions.WebRootPath, "uploads")
            : Path.Combine(webRoot, "uploads");

        if (!Directory.Exists(persistentUploadsDir))
        {
            Directory.CreateDirectory(persistentUploadsDir);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(persistentUploadsDir),
            RequestPath = "/uploads",
            OnPrepareResponse = ctx =>
            {
                var reqPath = ctx.Context.Request.Path.Value ?? string.Empty;
                // GAP-05: Block unauthenticated direct access to user progress photos via static file server.
                // Public demo assets (/uploads/progress/*.svg) remain accessible for initial page render.
                if (reqPath.Contains("/users/", StringComparison.OrdinalIgnoreCase) && reqPath.Contains("/progress/", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    ctx.Context.Response.ContentLength = 0;
                    ctx.Context.Response.Body = Stream.Null;
                }
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<UserPresenceMiddleware>();

        // Enforce Per-IP Partitioned Rate Limiting on authentication endpoints (OWASP A04)
        app.UseAppRateLimiter();

        // Native Health Check Endpoints (Azure Container Apps / Kubernetes Probes)
        app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
        app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        });

        // Controller Endpoints & SPA Fallback
        // Real-time In-App Notification SignalR Hub
        app.MapHub<Hubs.NotificationHub>("/hubs/notifications");

        app.MapControllers();
        app.MapFallbackToFile("index.html");

        return app;
    }
}

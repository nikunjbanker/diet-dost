/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Nutrition.WebGateway.Middleware;

/// <summary>
/// ASP.NET Core middleware that restricts inbound HTTP requests in deployed environments
/// strictly to authorized showcase and apex domains (dev.diet-dost.in and diet-dost.in).
/// In showcase/demo mode (Security:AllowDemoUsers == true), Azure Container Apps default
/// domain (*.azurecontainerapps.io) is also permitted for validation before DNS propagation.
/// </summary>
public class HostGatingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration? _configuration;

    public HostGatingMiddleware(RequestDelegate next, IHostEnvironment environment, IConfiguration? configuration = null)
    {
        _next = next;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var host = context.Request.Host.Host;
        var path = context.Request.Path.Value ?? string.Empty;

        // Permit internal health probes and loopback
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Strictly permit only authorized showcase & apex domains
        if (string.Equals(host, "dev.diet-dost.in", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "diet-dost.in", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // In demo/showcase mode, permit *.azurecontainerapps.io so ACA default URL works out-of-the-box before DNS propagation
        var allowDemoUsers = (_configuration?.GetValue<bool>("Security:AllowDemoUsers") ?? false) ||
                             string.Equals(Environment.GetEnvironmentVariable("Security__AllowDemoUsers"), "true", StringComparison.OrdinalIgnoreCase);

        if (allowDemoUsers && host.EndsWith(".azurecontainerapps.io", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"ForbiddenHost\",\"message\":\"Access denied. This service is strictly restricted to https://dev.diet-dost.in and http://diet-dost.in.\"}");
    }
}

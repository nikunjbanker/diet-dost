/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using System.Text;
using Nutrition.Application.Common;

namespace Nutrition.WebGateway.Middleware;

/// <summary>
/// ASP.NET Core middleware that captures HTTP request and response payloads for API endpoints
/// and enriches the current OpenTelemetry Activity span and structured logging.
/// This surfaces the payloads directly in the Aspire Dashboard Traces inspector.
/// </summary>
public partial class HttpPayloadTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpPayloadTelemetryMiddleware> _logger;
    private const int MaxPayloadCaptureBytes = 65536; // 64 KB safety limit

    [System.Text.RegularExpressions.GeneratedRegex(@"[^\w\-/\.]")]
    private static partial System.Text.RegularExpressions.Regex SafePathRegex();

    public HttpPayloadTelemetryMiddleware(RequestDelegate next, ILogger<HttpPayloadTelemetryMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only inspect API endpoints; skip static files, root HTML, and health checks
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // GAP-02: Sensitive endpoint protection - bypass body capture on auth, profile, and user management endpoints
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/profile", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var activity = Activity.Current;
        var sw = Stopwatch.StartNew();
        string requestPayload = string.Empty;

        // 1. Capture Request Metadata Only (never log or tag raw PHI/auth payloads)
        if (context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            requestPayload = $"[Multipart Form Upload: ContentLength={context.Request.ContentLength} bytes]";
            activity?.SetTag(NutritionTelemetry.TagHttpRequestPayload, requestPayload);
        }
        else if (context.Request.ContentLength > 0)
        {
            requestPayload = $"[Redacted Payload: {context.Request.ContentLength} bytes]";
            activity?.SetTag(NutritionTelemetry.TagHttpRequestPayload, requestPayload);
        }

        // 2. Execute downstream pipeline without buffering or logging raw PHI response bodies
        try
        {
            await _next(context);

            var contentType = context.Response.ContentType ?? string.Empty;
            var contentLength = context.Response.ContentLength ?? 0;
            activity?.SetTag(NutritionTelemetry.TagHttpResponsePayload, $"[Redacted Response: {contentLength} bytes, ContentType={contentType}]");
            activity?.SetTag(NutritionTelemetry.TagHttpResponseStatusCode, context.Response.StatusCode);
        }
        finally
        {
            sw.Stop();

            var sanitizedMethod = HttpMethods.IsGet(context.Request.Method) ? "GET"
                : HttpMethods.IsPost(context.Request.Method) ? "POST"
                : HttpMethods.IsPut(context.Request.Method) ? "PUT"
                : HttpMethods.IsDelete(context.Request.Method) ? "DELETE"
                : "OTHER";

            var safePath = SafePathRegex().Replace(path, "_");
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path} finished with {StatusCode} in {ElapsedMs:0.0}ms (PayloadBytes: {RequestBytes}/{ResponseBytes})",
                    sanitizedMethod,
                    safePath,
                    context.Response.StatusCode,
                    sw.Elapsed.TotalMilliseconds,
                    context.Request.ContentLength ?? 0,
                    context.Response.ContentLength ?? 0);
            }
        }
    }

    private static string SanitizeForLog(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "[empty]";
        }

        return input.Replace("\r", string.Empty).Replace("\n", " ");
    }
}

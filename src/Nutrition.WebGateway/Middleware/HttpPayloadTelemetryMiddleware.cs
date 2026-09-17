using System.Diagnostics;
using System.Text;
using Nutrition.Application.Common;

namespace Nutrition.WebGateway.Middleware;

/// <summary>
/// ASP.NET Core middleware that captures HTTP request and response payloads for API endpoints
/// and enriches the current OpenTelemetry Activity span and structured logging.
/// This surfaces the payloads directly in the Aspire Dashboard Traces inspector.
/// </summary>
public class HttpPayloadTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpPayloadTelemetryMiddleware> _logger;
    private const int MaxPayloadCaptureBytes = 65536; // 64 KB safety limit

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

        var activity = Activity.Current;
        var sw = Stopwatch.StartNew();
        string requestPayload = string.Empty;

        // 1. Capture Request Payload
        if (context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            requestPayload = $"[Multipart Form Upload: ContentLength={context.Request.ContentLength} bytes, ContentType={context.Request.ContentType}]";
            activity?.SetTag(NutritionTelemetry.TagHttpRequestPayload, requestPayload);
        }
        else if (context.Request.ContentLength > 0 &&
                 (context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ||
                  context.Request.ContentType?.Contains("text", StringComparison.OrdinalIgnoreCase) == true ||
                  context.Request.ContentType?.Contains("form-urlencoded", StringComparison.OrdinalIgnoreCase) == true))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (bodyText.Length > MaxPayloadCaptureBytes)
            {
                requestPayload = bodyText[..MaxPayloadCaptureBytes] + " ...[TRUNCATED]";
            }
            else
            {
                requestPayload = bodyText;
            }

            activity?.SetTag(NutritionTelemetry.TagHttpRequestPayload, requestPayload);
        }

        // 2. Capture Response Payload by swapping response stream
        var originalBodyStream = context.Response.Body;
        using var responseMemoryStream = new MemoryStream();
        context.Response.Body = responseMemoryStream;

        string responsePayload = string.Empty;

        try
        {
            await _next(context);

            responseMemoryStream.Position = 0;
            var contentType = context.Response.ContentType ?? string.Empty;

            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                context.Response.StatusCode >= 400)
            {
                using var respReader = new StreamReader(responseMemoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                var fullRespText = await respReader.ReadToEndAsync();

                if (fullRespText.Length > MaxPayloadCaptureBytes)
                {
                    responsePayload = fullRespText[..MaxPayloadCaptureBytes] + " ...[TRUNCATED]";
                }
                else
                {
                    responsePayload = fullRespText;
                }

                activity?.SetTag(NutritionTelemetry.TagHttpResponsePayload, responsePayload);
            }
            else if (responseMemoryStream.Length > 0)
            {
                responsePayload = $"[Binary / Stream response: {responseMemoryStream.Length} bytes, ContentType={contentType}]";
                activity?.SetTag(NutritionTelemetry.TagHttpResponsePayload, responsePayload);
            }

            activity?.SetTag(NutritionTelemetry.TagHttpResponseStatusCode, context.Response.StatusCode);

            // Copy the intercepted stream back so the client receives the response
            responseMemoryStream.Position = 0;
            await responseMemoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            sw.Stop();

            // Emit structured log with captured payloads
            _logger.LogInformation(
                "HTTP {Method} {Path} finished with {StatusCode} in {ElapsedMs:0.0}ms | RequestBody: {RequestBody} | ResponseBody: {ResponseBody}",
                context.Request.Method,
                path,
                context.Response.StatusCode,
                sw.Elapsed.TotalMilliseconds,
                string.IsNullOrWhiteSpace(requestPayload) ? "[empty]" : requestPayload,
                string.IsNullOrWhiteSpace(responsePayload) ? "[empty]" : responsePayload);
        }
    }
}

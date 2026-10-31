/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Features.Auth.Commands.ApproveQrSession;
using Nutrition.Application.Features.Auth.Commands.ExchangeQrTicket;
using Nutrition.Application.Features.Auth.Commands.InitiateQrSession;
using Nutrition.Application.Features.Auth.Commands.RecoverAccount;
using Nutrition.Application.Features.Auth.Commands.RegisterAnonymous;
using Nutrition.Application.Features.Auth.Commands.RejectQrSession;
using Nutrition.Application.Features.Auth.Commands.SimulateQrApproval;
using Nutrition.Application.Features.Auth.Queries.GetQrSessionStatus;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;
using Polly;
using Polly.RateLimiting;

namespace Nutrition.WebGateway.Controllers;

public record RegisterAnonymousRequest(
    string? DisplayName,
    bool AcceptTerms,
    bool AcceptHealthConsent);

public record InitiateQrRequest(
    string BrowserSecretHash,
    string? DesktopPublicKey);

public record ApproveQrRequest(
    string SessionId,
    string ChallengeNonce,
    string? MobilePublicKey,
    string? EncryptedApprovalPayload);

public record SimulateQrApprovalRequest(
    string SessionId,
    string ChallengeNonce,
    string? DemoEmail = null);

public record RejectQrRequest(
    string SessionId,
    string ChallengeNonce);

public record ExchangeQrRequest(
    string SessionId,
    string ExchangeTicket,
    string BrowserSecretToken);

public record RecoverAccountRequest(
    string RecoveryKeyOrMnemonic,
    bool RotateKey = false);

/// <summary>
/// Thin Presentation Controller for Cross-Device QR Authentication, Zero-PII Anonymous Onboarding,
/// and Emergency Disaster Recovery adhering strictly to NIST SP 800-63B and OWASP ASVS 4.0.
/// </summary>
[ApiController]
[Route("api/auth")]
public class QrAuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IConfiguration _configuration;
    private readonly IQrCodeGenerator _qrGenerator;
    private readonly ILogger<QrAuthController> _logger;
    private readonly ResiliencePipeline _rateLimiter;

    public QrAuthController(
        IDispatcher dispatcher,
        IConfiguration configuration,
        IQrCodeGenerator qrGenerator,
        ILogger<QrAuthController> logger,
        ResiliencePipeline rateLimiter)
    {
        _dispatcher = dispatcher;
        _configuration = configuration;
        _qrGenerator = qrGenerator;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    private async Task<IActionResult> ExecuteWithRateLimitAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await _rateLimiter.ExecuteAsync(async _ => await action());
        }
        catch (RateLimiterRejectedException)
        {
            _logger.LogWarning("Rate limit exceeded on QR auth endpoint for client IP: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Type = "https://dietdost.app/errors/rate-limit-exceeded",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Rate limit exceeded. Please wait before retrying cross-device authentication."
            });
        }
    }

    [HttpPost("register-anonymous")]
    public async Task<IActionResult> RegisterAnonymous([FromBody] RegisterAnonymousRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new RegisterAnonymousCommand(
                request.DisplayName,
                request.AcceptTerms,
                request.AcceptHealthConsent,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/anonymous-registration-failed",
                    Title = result.ErrorCode ?? "Registration Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            // Issue HttpOnly browser session cookie
            if (result.Data!.Principal != null)
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    result.Data.Principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                userId = result.Data!.UserId,
                displayName = result.Data.DisplayName,
                token = result.Data.Token,
                tokenType = "Bearer",
                recoveryKey = result.Data.RecoveryKeyHex,
                mnemonicPhrase = result.Data.MnemonicPhrase,
                recoveryLookupId = result.Data.RecoveryLookupId,
                jsonBackupPayload = result.Data.JsonBackupPayload,
                user = result.Data.User
            });
        });
    }

    [HttpPost("qr/initiate")]
    public async Task<IActionResult> InitiateQr([FromBody] InitiateQrRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var command = new InitiateQrSessionCommand(
                request.BrowserSecretHash,
                request.DesktopPublicKey,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                baseUrl
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/qr-initiate-failed",
                    Title = result.ErrorCode ?? "QR Initiation Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            return Ok(new
            {
                sessionId = result.Data!.SessionId,
                challengeNonce = result.Data.ChallengeNonce,
                confirmationCode = result.Data.ConfirmationCode,
                confirmationGlyph = result.Data.ConfirmationGlyph,
                qrPayloadUri = result.Data.QrPayloadUri,
                deepLinkUri = result.Data.DeepLinkUri,
                qrSvg = result.Data.QrSvg,
                dataUri = result.Data.DataUri,
                expiresAtUtc = result.Data.ExpiresAtUtc,
                ttlSeconds = result.Data.TtlSeconds
            });
        });
    }

    [HttpGet("qr/code/{sessionId}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetQrSvg(string sessionId, CancellationToken ct)
    {
        var query = new GetQrSessionStatusQuery(sessionId);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded || result.Data == null)
        {
            return NotFound();
        }

        var baseUri = $"{Request.Scheme}://{Request.Host}";
        var qrUri = $"{baseUri}/qr-approve.html?sid={result.Data.SessionId}";
        var svg = _qrGenerator.GenerateSvg(qrUri, size: 240, margin: 4);

        Response.Headers.ContentDisposition = "inline; filename=\"qr.svg\"";
        return Content(svg, "image/svg+xml");
    }

    [HttpGet("qr/status/{sessionId}")]
    public async Task<IActionResult> GetQrStatus(string sessionId, CancellationToken ct)
    {
        var query = new GetQrSessionStatusQuery(sessionId);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new ProblemDetails
            {
                Type = "https://dietdost.app/errors/qr-status-failed",
                Title = result.ErrorCode ?? "QR Status Failed",
                Status = result.StatusCode,
                Detail = result.Error
            });
        }

        var data = result.Data!;
        // Use ETag for efficient adaptive polling
        var etag = $"\"{data.SessionId}-{data.Status}-{(data.ExchangeTicket != null ? "1" : "0")}\"";
        if (Request.Headers.IfNoneMatch == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        return Ok(new
        {
            sessionId = data.SessionId,
            status = data.Status.ToString(),
            isExpired = data.IsExpired,
            exchangeTicket = data.ExchangeTicket,
            mobilePublicKey = data.MobilePublicKey,
            encryptedApprovalPayload = data.EncryptedApprovalPayload,
            clientIpAddress = data.ClientIpAddress,
            clientUserAgent = data.ClientUserAgent
        });
    }

    [HttpGet("qr/stream/{sessionId}")]
    public async Task GetQrStream(string sessionId, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("Connection", "keep-alive");

        var query = new GetQrSessionStatusQuery(sessionId);
        var lastStatus = string.Empty;

        for (int i = 0; i < 60; i++) // Max 90s (60 * 1.5s)
        {
            if (ct.IsCancellationRequested) break;

            var result = await _dispatcher.QueryAsync(query, ct);
            if (!result.Succeeded || result.Data == null)
            {
                await Response.WriteAsync($"event: error\ndata: {{\"error\": \"SessionNotFound\"}}\n\n", ct);
                await Response.Body.FlushAsync(ct);
                break;
            }

            var currentStatus = result.Data.Status.ToString();
            if (currentStatus != lastStatus)
            {
                lastStatus = currentStatus;
                var json = JsonSerializer.Serialize(new
                {
                    sessionId = result.Data.SessionId,
                    status = currentStatus,
                    exchangeTicket = result.Data.ExchangeTicket,
                    mobilePublicKey = result.Data.MobilePublicKey,
                    encryptedApprovalPayload = result.Data.EncryptedApprovalPayload
                });

                await Response.WriteAsync($"event: status\ndata: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            if (result.Data.Status == QrSessionStatus.Approved ||
                result.Data.Status == QrSessionStatus.Consumed ||
                result.Data.Status == QrSessionStatus.Expired ||
                result.Data.Status == QrSessionStatus.Rejected)
            {
                break;
            }

            await Task.Delay(1500, ct);
        }
    }

    [Authorize]
    [HttpPost("qr/approve")]
    public async Task<IActionResult> ApproveQr([FromBody] ApproveQrRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/unauthorized",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Mobile user session is invalid or missing."
                });
            }

            var command = new ApproveQrSessionCommand(
                request.SessionId,
                request.ChallengeNonce,
                userId,
                request.MobilePublicKey,
                request.EncryptedApprovalPayload
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/qr-approval-failed",
                    Title = result.ErrorCode ?? "Approval Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            return Ok(new
            {
                message = "QR Session successfully approved.",
                sessionId = request.SessionId,
                exchangeTicket = result.Data
            });
        });
    }

    [HttpPost("qr/simulate-approval")]
    public async Task<IActionResult> SimulateApproval([FromBody] SimulateQrApprovalRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new SimulateQrApprovalCommand(
                request.SessionId,
                request.ChallengeNonce,
                request.DemoEmail
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/qr-simulate-failed",
                    Title = result.ErrorCode ?? "Simulated Approval Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            return Ok(new
            {
                message = "Simulated mobile approval succeeded.",
                sessionId = request.SessionId,
                exchangeTicket = result.Data
            });
        });
    }

    [HttpPost("qr/reject")]
    public async Task<IActionResult> RejectQr([FromBody] RejectQrRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new RejectQrSessionCommand(
                request.SessionId,
                request.ChallengeNonce
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/qr-reject-failed",
                    Title = result.ErrorCode ?? "Rejection Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            return Ok(new
            {
                message = "QR Session successfully rejected.",
                sessionId = request.SessionId
            });
        });
    }

    [HttpPost("qr/exchange")]
    public async Task<IActionResult> ExchangeQr([FromBody] ExchangeQrRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new ExchangeQrTicketCommand(
                request.SessionId,
                request.ExchangeTicket,
                request.BrowserSecretToken
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/qr-exchange-failed",
                    Title = result.ErrorCode ?? "Exchange Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            if (result.Data!.Principal != null)
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    result.Data.Principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });
            }

            return Ok(new
            {
                message = "Successfully authenticated via cross-device QR handshake.",
                token = result.Data.Token,
                tokenType = "Bearer",
                user = result.Data.User
            });
        });
    }

    [HttpPost("recover")]
    public async Task<IActionResult> RecoverAccount([FromBody] RecoverAccountRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new RecoverAccountCommand(request.RecoveryKeyOrMnemonic, request.RotateKey);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new ProblemDetails
                {
                    Type = "https://dietdost.app/errors/account-recovery-failed",
                    Title = result.ErrorCode ?? "Recovery Failed",
                    Status = result.StatusCode,
                    Detail = result.Error
                });
            }

            if (result.Data!.Principal != null)
            {
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    result.Data.Principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });
            }

            return Ok(new
            {
                message = "Account successfully recovered. Prior sessions have been invalidated, and a new recovery kit has been generated.",
                token = result.Data.Token,
                tokenType = "Bearer",
                user = result.Data.User,
                newRecoveryKey = result.Data.NewRecoveryKeyHex,
                newMnemonicPhrase = result.Data.NewMnemonicPhrase,
                newRecoveryLookupId = result.Data.NewRecoveryLookupId,
                newJsonBackupPayload = result.Data.NewJsonBackupPayload
            });
        });
    }
}

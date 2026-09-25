using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Auth.Commands.DeleteAccount;
using Nutrition.Application.Features.Auth.Commands.ForgotPassword;
using Nutrition.Application.Features.Auth.Commands.Login;
using Nutrition.Application.Features.Auth.Commands.RegisterUser;
using Nutrition.Application.Features.Auth.Commands.ResendOtp;
using Nutrition.Application.Features.Auth.Commands.ResetPassword;
using Nutrition.Application.Features.Auth.Commands.VerifyOtp;
using Nutrition.Application.Features.Auth.Queries.GetCurrentUser;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;
using Polly;
using Polly.RateLimiting;

namespace Nutrition.WebGateway.Controllers;

public record RegisterRequest(
    string Name,
    string Email,
    string MobileNumber,
    string Password,
    bool AcceptTerms,
    bool AcceptHealthConsent);

public record VerifyOtpRequest(
    string Target,
    OtpChannel Channel,
    string Code);

public record ResendOtpRequest(
    string Target,
    OtpChannel Channel);

public record LoginRequest(
    string EmailOrMobile,
    string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(
    string Email,
    string OtpCode,
    string NewPassword,
    string ConfirmNewPassword);

/// <summary>
/// Thin Presentation Controller for Authentication & Identity.
/// Strictly handles HTTP semantics, rate limiting, and cookie headers;
/// dispatches all business use-cases to Application CQRS handlers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;
    private readonly ResiliencePipeline _rateLimiter;

    public AuthController(
        IDispatcher dispatcher,
        IWebHostEnvironment env,
        ILogger<AuthController> logger,
        ResiliencePipeline rateLimiter)
    {
        _dispatcher = dispatcher;
        _env = env;
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
            _logger.LogWarning("Rate limit exceeded on auth endpoint for client IP: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "TooManyRequests",
                message = "Too many authentication attempts. In accordance with OWASP security guidelines, please wait before retrying."
            });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var isDev = _env.IsDevelopment();
            var command = new RegisterUserCommand(
                request.Name,
                request.Email,
                request.MobileNumber,
                request.Password,
                request.AcceptTerms,
                request.AcceptHealthConsent,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                isDev
            );

            var result = await _dispatcher.SendAsync(command, ct);
            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            if (isDev && !string.IsNullOrWhiteSpace(result.Data!.DevOtpCode))
            {
                Response.Headers.Append("X-Dev-Otp-Code", result.Data.DevOtpCode);
            }

            return StatusCode(result.StatusCode, new
            {
                userId = result.Data!.UserId,
                email = result.Data.Email,
                message = result.Data.Message,
                devOtpCode = result.Data.DevOtpCode
            });
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new VerifyOtpCommand(request.Target, request.Channel, request.Code);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            // Issue HttpOnly browser session cookie if principal provided
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
                message = result.Data.Message,
                token = result.Data.Token,
                tokenType = "Bearer",
                expiresInSeconds = 86400,
                user = result.Data.User
            });
        });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var isDev = _env.IsDevelopment();
            var command = new ResendOtpCommand(request.Target, request.Channel, isDev);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            if (isDev && !string.IsNullOrWhiteSpace(result.Data!.DevOtpCode))
            {
                Response.Headers.Append("X-Dev-Otp-Code", result.Data.DevOtpCode);
            }

            return Ok(new
            {
                message = result.Data!.Message,
                devOtpCode = result.Data.DevOtpCode
            });
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new LoginCommand(request.EmailOrMobile, request.Password);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            // Sign-in HttpOnly cookie for browser clients
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Data!.Principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            return Ok(new
            {
                message = "Login successful.",
                token = result.Data.Token,
                tokenType = "Bearer",
                expiresInSeconds = 86400,
                user = result.Data.User
            });
        });
    }

    /// <summary>
    /// Programmatic token issuance endpoint for mobile apps and CLI clients.
    /// Returns a signed JWT bearer token directly without setting cookie state.
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> GenerateTokenDirect([FromBody] LoginRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new LoginCommand(request.EmailOrMobile, request.Password);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            return Ok(new
            {
                token = result.Data!.Token,
                tokenType = "Bearer",
                expiresInSeconds = 86400,
                user = result.Data.User
            });
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out successfully. Secure session terminated." });
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(new DeleteAccountCommand(userId), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = result.Data });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var isDev = _env.IsDevelopment();
            var command = new ForgotPasswordCommand(request.Email, isDev);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            if (isDev && !string.IsNullOrWhiteSpace(result.Data!.DevOtpCode))
            {
                Response.Headers.Append("X-Dev-Otp-Code", result.Data.DevOtpCode);
            }

            return Ok(new
            {
                message = result.Data!.Message,
                devOtpCode = result.Data.DevOtpCode
            });
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        return await ExecuteWithRateLimitAsync(async () =>
        {
            var command = new ResetPasswordCommand(request.Email, request.OtpCode, request.NewPassword, request.ConfirmNewPassword);
            var result = await _dispatcher.SendAsync(command, ct);

            if (!result.Succeeded)
            {
                return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
            }

            return Ok(new { message = result.Data });
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetCurrentUserQuery(userId), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }
}

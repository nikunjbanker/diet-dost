using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Extensions;

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

public record CurrentUserResponse(
    string Id,
    string Email,
    string MobileNumber,
    string Name,
    UserRole Role,
    UserTier Tier,
    bool IsEmailVerified,
    bool IsMobileVerified,
    TierFeatureConfiguration? Entitlements);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DietTrackerDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        DietTrackerDbContext db,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _config = config;
        _env = env;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.MobileNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "MissingFields", message = "Name, email, mobile phone number, and password are all required." });
        }

        if (!request.AcceptTerms || !request.AcceptHealthConsent)
        {
            return BadRequest(new
            {
                error = "LegalConsentRequired",
                message = "DPDPA 2023 Compliance Violation: Both the Terms & AI Model Training Agreement and the Sensitive Health Data Processing Consent must be affirmatively accepted."
            });
        }

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(request.Email);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(request.MobileNumber);

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (existingUser != null)
        {
            return Conflict(new { error = "EmailAlreadyRegistered", message = "An account with this email address already exists. Please log in or reset your password." });
        }

        var termsVersion = _config["Auth:TermsVersion"] ?? "v1.0-202609";
        var healthConsentVersion = _config["Auth:HealthConsentVersion"] ?? "v1.0-202609";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var userAgent = Request.Headers.UserAgent.ToString();

        var user = new ApplicationUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            MobileNumber = request.MobileNumber.Trim(),
            NormalizedMobileNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = false,
            IsMobileVerified = false,
            IsActive = true,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = termsVersion,
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = healthConsentVersion,
            ConsentIpAddress = ipAddress,
            ConsentUserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            user.ValidateRegistration();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationFailed", message = ex.Message });
        }

        await _db.Users.AddAsync(user, ct);

        // Pre-create clinical profile with standard default baseline
        var profile = new UserProfile
        {
            Id = user.Id,
            Name = request.Name.Trim(),
            Sex = BiologicalSex.Male,
            Age = 30,
            HeightCm = 170,
            CurrentWeightKg = 70,
            TargetWeightKg = 65,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Sedentary,
            DietaryPreference = DietaryPreference.LactoVeg,
            RegionalCuisine = "North Indian",
            Timezone = "Asia/Kolkata"
        };
        await _db.Profiles.AddAsync(profile, ct);

        // Generate 6-digit OTP code for Email verification
        var rawCode = _otpService.GenerateCode();
        var otp = _otpService.CreateOtp(user.Id, user.Email, OtpChannel.Email, rawCode);
        await _db.VerificationOtps.AddAsync(otp, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Registration completed for user: {UserId}. OTP dispatched to: {Target}", user.Id, user.Email);

        // In development mode, provide OTP in dev header & response for test harness automation
        var isDev = _env.IsDevelopment();
        if (isDev)
        {
            Response.Headers.Append("X-Dev-Otp-Code", rawCode);
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            userId = user.Id,
            email = user.Email,
            message = "Registration successful. Please enter the 6-digit verification code sent to your email to activate your account.",
            devOtpCode = isDev ? rawCode : null
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Target) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "MissingFields", message = "Target and verification code are required." });

        var targetTrimmed = request.Target.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(targetTrimmed);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(targetTrimmed);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
            return NotFound(new { error = "UserNotFound", message = "No user found associated with this email or mobile." });

        var now = DateTime.UtcNow;
        var activeOtp = await _db.VerificationOtps
            .Where(o => o.UserId == user.Id && !o.IsUsed && o.ExpiresAtUtc > now)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (activeOtp == null)
        {
            return BadRequest(new { error = "NoActiveOtp", message = "No active verification code found or code has expired. Please request a new code." });
        }

        var isValid = _otpService.ValidateCode(activeOtp, request.Code, out var failureReason);
        if (!isValid)
        {
            await _db.SaveChangesAsync(ct);
            return BadRequest(new { error = "InvalidOtp", message = failureReason, attemptsRemaining = VerificationOtp.MaxAttemptsAllowed - activeOtp.AttemptCount });
        }

        if (request.Channel == OtpChannel.Email || activeOtp.Channel == OtpChannel.Email)
        {
            user.IsEmailVerified = true;
        }
        else
        {
            user.IsMobileVerified = true;
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        await SignInUserAsync(user, profile?.Name ?? user.Email);

        var tierConfig = await _db.TierConfigurations.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        _logger.LogInformation("[AUTH] Account verified and logged in: {UserId}", user.Id);

        return Ok(new
        {
            message = "Account successfully verified and activated.",
            user = new CurrentUserResponse(
                user.Id,
                user.Email,
                user.MobileNumber,
                profile?.Name ?? user.Email,
                user.Role,
                user.Tier,
                user.IsEmailVerified,
                user.IsMobileVerified,
                tierConfig)
        });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
            return BadRequest(new { error = "MissingFields", message = "Target email or mobile number is required." });

        var targetTrimmed = request.Target.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(targetTrimmed);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(targetTrimmed);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
            return NotFound(new { error = "UserNotFound", message = "No user found associated with this email or mobile." });

        // Cooldown check: prevent generating more than 1 code per 60 seconds
        var recentOtp = await _db.VerificationOtps
            .Where(o => o.UserId == user.Id && o.CreatedAtUtc > DateTime.UtcNow.AddSeconds(-60))
            .FirstOrDefaultAsync(ct);

        if (recentOtp != null)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "CooldownActive",
                message = "Please wait at least 60 seconds before requesting another verification code."
            });
        }

        // Expire older active OTPs
        var existingOtps = await _db.VerificationOtps
            .Where(o => o.UserId == user.Id && !o.IsUsed)
            .ToListAsync(ct);
        foreach (var old in existingOtps)
        {
            old.IsUsed = true;
        }

        var rawCode = _otpService.GenerateCode();
        var newOtp = _otpService.CreateOtp(user.Id, user.Email, request.Channel, rawCode);
        await _db.VerificationOtps.AddAsync(newOtp, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Resent OTP for user: {UserId}. Target: {Target}", user.Id, user.Email);

        var isDev = _env.IsDevelopment();
        if (isDev)
        {
            Response.Headers.Append("X-Dev-Otp-Code", rawCode);
        }

        return Ok(new
        {
            message = "A fresh 6-digit verification code has been dispatched.",
            devOtpCode = isDev ? rawCode : null
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrMobile) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "MissingFields", message = "Email/mobile and password are required." });

        var identifier = request.EmailOrMobile.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(identifier);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(identifier);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
        {
            return Unauthorized(new { error = "InvalidCredentials", message = "Invalid email/mobile or password." });
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Unauthorized(new { error = "InvalidCredentials", message = "Invalid email/mobile or password." });
        }

        if (!user.CanLogin(out var rejectionReason))
        {
            if (!user.IsEmailVerified)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "UnverifiedAccount",
                    message = rejectionReason,
                    email = user.Email
                });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "AccountDeactivated",
                message = rejectionReason
            });
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        await SignInUserAsync(user, profile?.Name ?? user.Email);

        var tierConfig = await _db.TierConfigurations.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        _logger.LogInformation("[AUTH] User logged in successfully: {UserId}, Tier: {Tier}", user.Id, user.Tier);

        return Ok(new
        {
            message = "Login successful.",
            user = new CurrentUserResponse(
                user.Id,
                user.Email,
                user.MobileNumber,
                profile?.Name ?? user.Email,
                user.Role,
                user.Tier,
                user.IsEmailVerified,
                user.IsMobileVerified,
                tierConfig)
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { isAuthenticated = false });
        }

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { isAuthenticated = false });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || !user.IsActive)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(new { isAuthenticated = false });
        }

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        var tierConfig = await _db.TierConfigurations.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        return Ok(new
        {
            isAuthenticated = true,
            user = new CurrentUserResponse(
                user.Id,
                user.Email,
                user.MobileNumber,
                profile?.Name ?? user.Email,
                user.Role,
                user.Tier,
                user.IsEmailVerified,
                user.IsMobileVerified,
                tierConfig)
        });
    }

    [Authorize]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return NotFound();

        if (user.Role == UserRole.SuperAdmin)
        {
            return BadRequest(new { error = "CannotDeleteSuperAdmin", message = "The primary SuperAdmin account cannot be deleted." });
        }

        // DPDPA 2023 Right to Erasure: Cascade purge of all user records
        var meals = await _db.Meals.Where(m => m.UserId == userId).ToListAsync(ct);
        _db.Meals.RemoveRange(meals);

        var ledgers = await _db.Ledgers.Where(l => l.UserId == userId).ToListAsync(ct);
        _db.Ledgers.RemoveRange(ledgers);

        var photos = await _db.ProgressPhotos.Where(p => p.UserId == userId).ToListAsync(ct);
        _db.ProgressPhotos.RemoveRange(photos);

        var feedbacks = await _db.AiFeedbacks.Where(f => f.UserId == userId).ToListAsync(ct);
        _db.AiFeedbacks.RemoveRange(feedbacks);

        var corrections = await _db.Corrections.Where(c => c.UserId == userId).ToListAsync(ct);
        _db.Corrections.RemoveRange(corrections);

        var otps = await _db.VerificationOtps.Where(o => o.UserId == userId).ToListAsync(ct);
        _db.VerificationOtps.RemoveRange(otps);

        var aiLogs = await _db.AiUsageLogs.Where(l => l.UserId == userId).ToListAsync(ct);
        _db.AiUsageLogs.RemoveRange(aiLogs);

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (profile != null)
            _db.Profiles.Remove(profile);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogWarning("[AUTH] User account and all health data purged under DPDPA Right to Erasure: {UserId}", userId);

        return Ok(new { message = "Your account and all associated health records have been permanently deleted per DPDPA guidelines." });
    }

    private async Task SignInUserAsync(ApplicationUser user, string displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tier", user.Tier.ToString()),
            new("security_stamp", user.SecurityStamp)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }
}

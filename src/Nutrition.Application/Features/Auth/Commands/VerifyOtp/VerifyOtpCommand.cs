using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Target,
    OtpChannel Channel,
    string Code
) : ICommand<Result<VerifyOtpResultDto>>;

public class VerifyOtpCommandHandler : ICommandHandler<VerifyOtpCommand, Result<VerifyOtpResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;

    public VerifyOtpCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<VerificationOtp> otpRepo,
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork uow,
        IOtpService otpService,
        IJwtTokenService jwtTokenService,
        ILogger<VerifyOtpCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _otpRepo = otpRepo;
        _tierRepo = tierRepo;
        _uow = uow;
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<VerifyOtpResultDto>> HandleAsync(VerifyOtpCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Target) || string.IsNullOrWhiteSpace(request.Code))
            return Result<VerifyOtpResultDto>.Failure("Target and verification code are required.", "MissingFields", 400);

        var targetTrimmed = request.Target.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(targetTrimmed);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(targetTrimmed);

        var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
            return Result<VerifyOtpResultDto>.Failure("No user found associated with this email or mobile.", "UserNotFound", 404);

        var now = DateTime.UtcNow;
        var otps = await _otpRepo.FindAsync(o => o.UserId == user.Id && !o.IsUsed && o.ExpiresAtUtc > now, ct);
        var activeOtp = otps.OrderByDescending(o => o.CreatedAtUtc).FirstOrDefault();

        if (activeOtp == null)
        {
            return Result<VerifyOtpResultDto>.Failure("No active verification code found or code has expired. Please request a new code.", "NoActiveOtp", 400);
        }

        var isValid = _otpService.ValidateCode(activeOtp, request.Code, out var failureReason);
        if (!isValid)
        {
            await _uow.SaveChangesAsync(ct);
            return Result<VerifyOtpResultDto>.Failure(failureReason ?? "Invalid verification code.", "InvalidOtp", 400);
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
        await _uow.SaveChangesAsync(ct);

        var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        var displayName = profile?.Name ?? user.Email;

        var token = _jwtTokenService.GenerateToken(user, displayName);
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        _logger.LogInformation("[AUTH] Account verified and logged in: {UserId}", user.Id);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tier", user.Tier.ToString()),
            new("security_stamp", user.SecurityStamp)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "SmartScheme"));

        var responseUser = new CurrentUserResponseDto(
            user.Id,
            user.Email,
            user.MobileNumber,
            displayName,
            user.Role,
            user.Tier,
            user.IsEmailVerified,
            user.IsMobileVerified,
            tierConfig
        );

        return Result<VerifyOtpResultDto>.Success(new VerifyOtpResultDto(
            "Account successfully verified and activated.",
            token,
            responseUser,
            principal
        ));
    }
}

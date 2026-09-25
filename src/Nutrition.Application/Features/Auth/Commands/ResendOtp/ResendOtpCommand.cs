using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.ResendOtp;

public record ResendOtpCommand(
    string Target,
    OtpChannel Channel,
    bool IsDev
) : ICommand<Result<ResendOtpResultDto>>;

public record ResendOtpResultDto(
    string Message,
    string? DevOtpCode
);

public class ResendOtpCommandHandler : ICommandHandler<ResendOtpCommand, Result<ResendOtpResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otpService;
    private readonly ILogger<ResendOtpCommandHandler> _logger;

    public ResendOtpCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IOtpService otpService,
        ILogger<ResendOtpCommandHandler> logger)
    {
        _userRepo = userRepo;
        _otpRepo = otpRepo;
        _uow = uow;
        _otpService = otpService;
        _logger = logger;
    }

    public async Task<Result<ResendOtpResultDto>> HandleAsync(ResendOtpCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
            return Result<ResendOtpResultDto>.Failure("Target email or mobile number is required.", "MissingFields", 400);

        var targetTrimmed = request.Target.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(targetTrimmed);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(targetTrimmed);

        var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
            return Result<ResendOtpResultDto>.Failure("No user found associated with this email or mobile.", "UserNotFound", 404);

        // Cooldown check: prevent generating more than 1 code per 60 seconds
        var threshold = DateTime.UtcNow.AddSeconds(-60);
        var recentOtps = await _otpRepo.FindAsync(o => o.UserId == user.Id && o.CreatedAtUtc > threshold, ct);
        if (recentOtps.Any())
        {
            return Result<ResendOtpResultDto>.Failure("Please wait at least 60 seconds before requesting another verification code.", "CooldownActive", 429);
        }

        // Expire older active OTPs
        var existingOtps = await _otpRepo.FindAsync(o => o.UserId == user.Id && !o.IsUsed, ct);
        foreach (var old in existingOtps)
        {
            old.IsUsed = true;
        }

        var rawCode = _otpService.GenerateCode();
        var destination = request.Channel == OtpChannel.Email ? user.Email : user.MobileNumber;
        var otp = _otpService.CreateOtp(user.Id, destination, request.Channel, rawCode);
        await _otpRepo.AddAsync(otp, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Resent OTP dispatched for user: {UserId}, Channel: {Channel}", user.Id, request.Channel);

        return Result<ResendOtpResultDto>.Success(new ResendOtpResultDto(
            $"A new verification code has been dispatched to your {request.Channel.ToString().ToLowerInvariant()}.",
            request.IsDev ? rawCode : null
        ));
    }
}

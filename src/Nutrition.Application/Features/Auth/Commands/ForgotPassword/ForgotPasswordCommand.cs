/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(
    string Email,
    bool IsDev
) : ICommand<Result<ForgotPasswordResultDto>>;

public class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, Result<ForgotPasswordResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otpService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IOtpService otpService,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepo = userRepo;
        _otpRepo = otpRepo;
        _uow = uow;
        _otpService = otpService;
        _logger = logger;
    }

    public async Task<Result<ForgotPasswordResultDto>> HandleAsync(ForgotPasswordCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<ForgotPasswordResultDto>.Failure("Email address is required.", "MissingFields", 400);

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(request.Email);
        var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        // Anti-enumeration (OWASP A07): always acknowledge generically
        if (user == null || !user.IsActive || !user.IsEmailVerified)
        {
            _logger.LogInformation("[AUTH] ForgotPassword: no eligible active user for email {Email}", request.Email);
            return Result<ForgotPasswordResultDto>.Success(new ForgotPasswordResultDto(
                "If a matching active account exists, a reset code has been sent to its email address.",
                null
            ));
        }

        // Expire any outstanding OTPs for this user
        var existingOtps = await _otpRepo.FindAsync(o => o.UserId == user.Id && !o.IsUsed, ct);
        foreach (var old in existingOtps)
        {
            old.IsUsed = true;
        }

        var rawCode = _otpService.GenerateCode();
        var otp = _otpService.CreateOtp(user.Id, user.Email, OtpChannel.Email, rawCode);
        await _otpRepo.AddAsync(otp, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] ForgotPassword OTP dispatched for user: {UserId}", user.Id);

        return Result<ForgotPasswordResultDto>.Success(new ForgotPasswordResultDto(
            "If a matching active account exists, a reset code has been sent to its email address.",
            request.IsDev ? rawCode : null
        ));
    }
}

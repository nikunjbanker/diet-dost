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
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string OtpCode,
    string NewPassword,
    string ConfirmNewPassword
) : ICommand<Result<string>>;

public class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result<string>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userRepo = userRepo;
        _otpRepo = otpRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _logger = logger;
    }

    public async Task<Result<string>> HandleAsync(ResetPasswordCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.OtpCode) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
        {
            return Result<string>.Failure("Email, OTP code, new password, and confirmation are all required.", "MissingFields", 400);
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return Result<string>.Failure("New password and confirmation password do not match.", "PasswordMismatch", 400);
        }

        if (!PasswordPolicy.IsValid(request.NewPassword, out var pwdError))
        {
            return Result<string>.Failure(pwdError ?? "Password does not meet complexity requirements.", "WeakPassword", 400);
        }

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(request.Email);
        var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user == null || !user.IsActive)
        {
            return Result<string>.Failure("No eligible active account found for password reset.", "UserNotFound", 404);
        }

        var now = DateTime.UtcNow;
        var otps = await _otpRepo.FindAsync(o => o.UserId == user.Id && !o.IsUsed && o.ExpiresAtUtc > now, ct);
        var activeOtp = otps.OrderByDescending(o => o.CreatedAtUtc).FirstOrDefault();

        if (activeOtp == null)
        {
            return Result<string>.Failure("No active password reset code found or code has expired. Please initiate forgot-password again.", "NoActiveOtp", 400);
        }

        var isValid = _otpService.ValidateCode(activeOtp, request.OtpCode, out var failureReason);
        if (!isValid)
        {
            await _uow.SaveChangesAsync(ct);
            return Result<string>.Failure(failureReason ?? "Invalid verification code.", "InvalidOtp", 400);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // Invalidate existing sessions
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Password reset completed for user: {UserId}", user.Id);

        return Result<string>.Success("Password has been successfully reset. Please sign in with your new password.");
    }
}

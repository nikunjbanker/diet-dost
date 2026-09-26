/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.RegisterUser;

public record RegisterUserCommand(
    string Name,
    string Email,
    string MobileNumber,
    string Password,
    bool AcceptTerms,
    bool AcceptHealthConsent,
    string? IpAddress,
    string? UserAgent,
    bool IsDev
) : ICommand<Result<RegisterResultDto>>;

public class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<RegisterResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<VerificationOtp> _otpRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IConfiguration _config;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IConfiguration config,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _otpRepo = otpRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _config = config;
        _logger = logger;
    }

    public async Task<Result<RegisterResultDto>> HandleAsync(RegisterUserCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.MobileNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<RegisterResultDto>.Failure("Name, email, mobile phone number, and password are all required.", "MissingFields", 400);
        }

        if (!request.AcceptTerms || !request.AcceptHealthConsent)
        {
            return Result<RegisterResultDto>.Failure(
                "DPDPA 2023 Compliance Violation: Both the Terms & AI Model Training Agreement and the Sensitive Health Data Processing Consent must be affirmatively accepted.",
                "LegalConsentRequired",
                400);
        }

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(request.Email);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(request.MobileNumber);

        var existingUser = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (existingUser != null)
        {
            return Result<RegisterResultDto>.Failure("An account with this email address already exists. Please log in or reset your password.", "EmailAlreadyRegistered", 409);
        }

        var termsVersion = _config["Auth:TermsVersion"] ?? "v1.0-202609";
        var healthConsentVersion = _config["Auth:HealthConsentVersion"] ?? "v1.0-202609";
        var ipAddress = request.IpAddress ?? "127.0.0.1";
        var userAgent = request.UserAgent ?? "Unknown";

        // Password policy enforcement (before hashing)
        if (!PasswordPolicy.IsValid(request.Password, out var pwdError))
        {
            return Result<RegisterResultDto>.Failure(pwdError ?? "Password does not meet strength requirements.", "WeakPassword", 400);
        }

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
            return Result<RegisterResultDto>.Failure(ex.Message, "ValidationFailed", 400);
        }

        await _userRepo.AddAsync(user, ct);

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
        await _profileRepo.AddAsync(profile, ct);

        // Generate 6-digit OTP code for Email verification
        var rawCode = _otpService.GenerateCode();
        var otp = _otpService.CreateOtp(user.Id, user.Email, OtpChannel.Email, rawCode);
        await _otpRepo.AddAsync(otp, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Registration completed for user: {UserId}. OTP dispatched to: {Target}", user.Id, user.Email);

        var responseDto = new RegisterResultDto(
            user.Id,
            user.Email,
            "Registration successful. Please enter the 6-digit verification code sent to your email to activate your account.",
            request.IsDev ? rawCode : null
        );

        return Result<RegisterResultDto>.Success(responseDto, 201);
    }
}

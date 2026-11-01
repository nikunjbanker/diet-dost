/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Common.Options;
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
    private readonly AuthOptions _authOptions;
    private readonly IInAppNotificationService? _notificationService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IOptions<AuthOptions> authOptions,
        ILogger<RegisterUserCommandHandler> logger)
        : this(userRepo, profileRepo, otpRepo, uow, passwordHasher, otpService, authOptions, null, logger)
    {
    }

    public RegisterUserCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<VerificationOtp> otpRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IOptions<AuthOptions> authOptions,
        IInAppNotificationService? notificationService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _otpRepo = otpRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _authOptions = authOptions?.Value ?? new AuthOptions();
        _notificationService = notificationService;
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

        var termsVersion = _authOptions.TermsVersion ?? "v1.0-202609";
        var healthConsentVersion = _authOptions.HealthConsentVersion ?? "v1.0-202609";
        var ipAddress = request.IpAddress ?? "127.0.0.1";
        var userAgent = request.UserAgent ?? "Unknown";

        // Password policy enforcement (before hashing)
        if (!PasswordPolicy.IsValid(request.Password, out var pwdError))
        {
            return Result<RegisterResultDto>.Failure(pwdError ?? "Password does not meet strength requirements.", "WeakPassword", 400);
        }

        // Alpha Preview Access Gate:
        // New user accounts require manual activation by the application owner (nikunj_banker@hotmail.com).
        // Therefore, IsActive is set to false initially.
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
            IsActive = false, // Alpha Preview: requires owner activation
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

        // Generate 6-digit OTP code for future verification
        var rawCode = _otpService.GenerateCode();
        var otp = _otpService.CreateOtp(user.Id, user.Email, OtpChannel.Email, rawCode);
        await _otpRepo.AddAsync(otp, ct);

        await _uow.SaveChangesAsync(ct);

        // Dispatch in-app notification to SuperAdmin
        var registrationTicket = Guid.NewGuid().ToString("N");
        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifySuperAdminNewRegistrationAsync(
                    user.Id,
                    request.Name.Trim(),
                    user.Email,
                    user.MobileNumber.Trim(),
                    user.CreatedAtUtc,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NOTIFICATIONS] Failed to dispatch in-app notification for new registration: {UserId}", user.Id);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[AUTH] Registration completed for user: {UserId}. Pending SuperAdmin approval.", user.Id);
        }

        var responseDto = new RegisterResultDto(
            user.Id,
            user.Email,
            "Registration received! Diet-Dost is currently in private Alpha preview. Your account is pending review and activation by the application owner (nikunj_banker@hotmail.com). You will be notified in the app once enabled.",
            request.IsDev ? rawCode : null,
            RequiresOwnerApproval: true,
            RegistrationTicket: registrationTicket
        );

        return Result<RegisterResultDto>.Success(responseDto, 201);
    }
}

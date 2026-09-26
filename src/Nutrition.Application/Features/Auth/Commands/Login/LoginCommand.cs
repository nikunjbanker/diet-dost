/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string EmailOrMobile,
    string Password
) : ICommand<Result<LoginResultDto>>;

public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAppEnvironment appEnvironment,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _tierRepo = tierRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _appEnvironment = appEnvironment;
        _logger = logger;
    }

    public async Task<Result<LoginResultDto>> HandleAsync(LoginCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrMobile) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResultDto>.Failure("Email/mobile and password are required.", "MissingFields", 400);
        }

        var identifier = request.EmailOrMobile.Trim();
        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(identifier);
        var normalizedPhone = ApplicationUser.NormalizePhoneNumber(identifier);

        var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedMobileNumber == normalizedPhone, ct);
        if (user == null)
        {
            return Result<LoginResultDto>.Failure("Invalid email/mobile or password.", "InvalidCredentials", 401);
        }

        var isDemo = user.IsDemoAccount || ApplicationUser.IsDemoEmail(identifier);
        if (isDemo && !_appEnvironment.AllowsDemoUsers)
        {
            _logger.LogWarning("[SECURITY] Blocked login attempt to demo account outside Debug/Development mode: {Email}", user.Email);
            return Result<LoginResultDto>.Failure("Demo accounts are strictly disabled in Release mode to prevent data breach.", "DemoAccessForbidden", 403);
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result<LoginResultDto>.Failure("Invalid email/mobile or password.", "InvalidCredentials", 401);
        }

        if (!user.CanLogin(out var rejectionReason))
        {
            if (!user.IsEmailVerified)
            {
                return Result<LoginResultDto>.Failure(rejectionReason ?? "Account email verification required.", "UnverifiedAccount", 403);
            }

            return Result<LoginResultDto>.Failure(rejectionReason ?? "Account is inactive or suspended.", "AccountDeactivated", 403);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        var displayName = profile?.Name ?? user.Email;

        var token = _jwtTokenService.GenerateToken(user, displayName);
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

        _logger.LogInformation("[AUTH] User logged in successfully: {UserId}, Tier: {Tier}", user.Id, user.Tier);

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

        return Result<LoginResultDto>.Success(new LoginResultDto(token, responseUser, principal));
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Auth.Commands.ExchangeQrTicket;

public record ExchangeQrTicketCommand(
    string SessionId,
    string ExchangeTicket,
    string BrowserSecretToken
) : ICommand<Result<ExchangeQrTicketResultDto>>;

public class ExchangeQrTicketCommandHandler : ICommandHandler<ExchangeQrTicketCommand, Result<ExchangeQrTicketResultDto>>
{
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<ExchangeQrTicketCommandHandler> _logger;

    public ExchangeQrTicketCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<TierFeatureConfiguration> tierRepo,
        IUnitOfWork uow,
        IJwtTokenService jwtTokenService,
        ILogger<ExchangeQrTicketCommandHandler> logger)
    {
        _qrRepo = qrRepo;
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _tierRepo = tierRepo;
        _uow = uow;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<ExchangeQrTicketResultDto>> HandleAsync(ExchangeQrTicketCommand request, CancellationToken ct = default)
    {
        var session = await _qrRepo.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session == null)
        {
            return Result<ExchangeQrTicketResultDto>.Failure("QR Session not found.", "QrSessionNotFound", 404);
        }

        if (session.IsExpired)
        {
            return Result<ExchangeQrTicketResultDto>.Failure("QR Session has expired.", "QrSessionExpired", 410);
        }

        if (session.Status == QrSessionStatus.Consumed)
        {
            return Result<ExchangeQrTicketResultDto>.Failure("This QR exchange ticket has already been consumed.", "TicketAlreadyConsumed", 409);
        }

        if (session.Status != QrSessionStatus.Approved || string.IsNullOrWhiteSpace(session.ExchangeTicket))
        {
            return Result<ExchangeQrTicketResultDto>.Failure("QR session is not in approved state.", "SessionNotApproved", 400);
        }

        if (!string.Equals(session.ExchangeTicket, request.ExchangeTicket, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ExchangeQrTicketResultDto>.Failure("Invalid exchange ticket.", "InvalidExchangeTicket", 401);
        }

        // Verify BrowserSecretToken matches stored BrowserSecretHash in constant time
        if (string.IsNullOrWhiteSpace(request.BrowserSecretToken))
        {
            return Result<ExchangeQrTicketResultDto>.Failure("Browser secret token is mandatory for dual-token redemption.", "MissingBrowserSecretToken", 401);
        }

        var candidateHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.BrowserSecretToken.Trim()));
        var candidateHashHex = Convert.ToHexString(candidateHashBytes).ToLowerInvariant();

        var candidateSpan = Encoding.UTF8.GetBytes(candidateHashHex);
        var storedSpan = Encoding.UTF8.GetBytes(session.BrowserSecretHash);

        if (!CryptographicOperations.FixedTimeEquals(candidateSpan, storedSpan))
        {
            _logger.LogWarning("[QR-AUTH] Shoulder-surfing / ticket theft attempt blocked on session {SessionId}. Browser secret mismatch.", session.Id);
            return Result<ExchangeQrTicketResultDto>.Failure("Browser secret token mismatch. Potential shoulder surfing blocked.", "BrowserSecretMismatch", 401);
        }

        // Consume session
        session.Consume();

        var user = await _userRepo.FirstOrDefaultAsync(u => u.Id == session.AuthorizedUserId, ct);
        if (user == null || user.IsDeleted || !user.IsActive)
        {
            return Result<ExchangeQrTicketResultDto>.Failure("User account is inactive or not found.", "UserInactive", 403);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == user.Id, ct);
        var displayName = profile?.Name ?? user.DisplayName ?? user.Email;

        var token = _jwtTokenService.GenerateToken(user, displayName);
        var tierConfig = await _tierRepo.FirstOrDefaultAsync(t => t.Tier == user.Tier, ct);

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

        _logger.LogInformation("[QR-AUTH] Session {SessionId} successfully exchanged for JWT for user {UserId}", session.Id, user.Id);

        return Result<ExchangeQrTicketResultDto>.Success(new ExchangeQrTicketResultDto(
            token,
            responseUser,
            principal
        ));
    }
}

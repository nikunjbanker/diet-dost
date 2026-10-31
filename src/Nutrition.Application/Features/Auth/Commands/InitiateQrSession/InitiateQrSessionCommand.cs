/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Auth.DTOs;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Auth.Commands.InitiateQrSession;

public record InitiateQrSessionCommand(
    string BrowserSecretHash,
    string? DesktopPublicKey = null,
    string? ClientIp = null,
    string? UserAgent = null,
    string? BaseUrl = null
) : ICommand<Result<InitiateQrSessionResultDto>>;

public class InitiateQrSessionCommandHandler : ICommandHandler<InitiateQrSessionCommand, Result<InitiateQrSessionResultDto>>
{
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IUnitOfWork _uow;
    private readonly IQrCodeGenerator _qrGenerator;
    private readonly ILogger<InitiateQrSessionCommandHandler> _logger;

    private static readonly string[] ConfirmationGlyphs = new[]
    {
        "Emerald Falcon",
        "Sapphire Tiger",
        "Ruby Dragon",
        "Golden Eagle",
        "Amethyst Wolf",
        "Diamond Leopard",
        "Amber Cheetah",
        "Topaz Cobra",
        "Jade Phoenix",
        "Onyx Panther"
    };

    public InitiateQrSessionCommandHandler(
        IRepository<QrLoginSession> qrRepo,
        IUnitOfWork uow,
        IQrCodeGenerator qrGenerator,
        ILogger<InitiateQrSessionCommandHandler> logger)
    {
        _qrRepo = qrRepo;
        _uow = uow;
        _qrGenerator = qrGenerator;
        _logger = logger;
    }

    public async Task<Result<InitiateQrSessionResultDto>> HandleAsync(InitiateQrSessionCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.BrowserSecretHash))
        {
            return Result<InitiateQrSessionResultDto>.Failure(
                "Browser memory secret hash is required for dual-token cryptographic handshake.",
                "BrowserSecretHashRequired",
                400);
        }

        var nonceBytes = new byte[16];
        RandomNumberGenerator.Fill(nonceBytes);
        var challengeNonce = Convert.ToHexString(nonceBytes).ToLowerInvariant();

        // 2-digit confirmation code 10..99
        var confirmationCode = RandomNumberGenerator.GetInt32(10, 100).ToString();
        var glyphIndex = RandomNumberGenerator.GetInt32(0, ConfirmationGlyphs.Length);
        var confirmationGlyph = ConfirmationGlyphs[glyphIndex];

        const int ttlSeconds = 90;
        var session = QrLoginSession.CreateNew(
            challengeNonce,
            request.BrowserSecretHash.Trim().ToLowerInvariant(),
            confirmationCode,
            confirmationGlyph,
            ttlSeconds,
            request.DesktopPublicKey,
            request.ClientIp,
            request.UserAgent
        );

        await _qrRepo.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);

        var baseUri = "https://dev.diet-dost.in";
        if (!string.IsNullOrWhiteSpace(request.BaseUrl) &&
            Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var parsedUri))
        {
            var host = parsedUri.Host.ToLowerInvariant();
            if (host == "dev.diet-dost.in" ||
                host == "diet-dost.in" ||
                host.EndsWith(".diet-dost.in") ||
                host == "localhost" ||
                host == "127.0.0.1")
            {
                baseUri = $"{parsedUri.Scheme}://{parsedUri.Authority}";
            }
            else
            {
                _logger.LogWarning("[QR_AUTH] Host header / BaseUrl '{BaseUrl}' rejected as untrusted domain. Falling back to default: {DefaultBaseUri}", request.BaseUrl, baseUri);
            }
        }

        var qrUri = $"{baseUri}/qr-approve.html?sid={session.Id}&nonce={session.ChallengeNonce}&code={session.ConfirmationCode}&glyph={Uri.EscapeDataString(session.ConfirmationGlyph)}";
        var deepLinkUri = $"dietdost://auth/qr?session={session.Id}&nonce={session.ChallengeNonce}&code={session.ConfirmationCode}&glyph={Uri.EscapeDataString(session.ConfirmationGlyph)}";
        if (!string.IsNullOrWhiteSpace(session.DesktopPublicKey))
        {
            qrUri += $"&pk={Uri.EscapeDataString(session.DesktopPublicKey)}";
            deepLinkUri += $"&pk={Uri.EscapeDataString(session.DesktopPublicKey)}";
        }

        var qrSvg = _qrGenerator.GenerateSvg(qrUri, size: 240, margin: 4);
        var dataUri = _qrGenerator.GenerateDataUri(qrUri, size: 240, margin: 4);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[QR-AUTH] Initiated QR session {SessionId} with MatchCode {Code} [{Glyph}]", session.Id, session.ConfirmationCode, session.ConfirmationGlyph);
        }

        return Result<InitiateQrSessionResultDto>.Success(new InitiateQrSessionResultDto(
            session.Id,
            session.ChallengeNonce,
            session.ConfirmationCode,
            session.ConfirmationGlyph,
            qrUri,
            session.ExpiresAtUtc,
            ttlSeconds,
            deepLinkUri,
            qrSvg,
            dataUri
        ));
    }
}

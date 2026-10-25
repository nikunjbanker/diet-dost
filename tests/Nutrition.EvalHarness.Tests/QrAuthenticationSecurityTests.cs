/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Features.Auth.Commands.ApproveQrSession;
using Nutrition.Application.Features.Auth.Commands.ExchangeQrTicket;
using Nutrition.Application.Features.Auth.Commands.InitiateQrSession;
using Nutrition.Application.Features.Auth.Commands.RecoverAccount;
using Nutrition.Application.Features.Auth.Commands.RegisterAnonymous;
using Nutrition.Application.Features.Auth.Queries.GetQrSessionStatus;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Comprehensive adversarial and security test suite validating NIST SP 800-63B (AAL3),
/// OWASP ASVS 4.0 Level 3, and DPDPA 2023 compliance for Cross-Device QR Authentication,
/// Zero-PII Anonymous Onboarding, and Disaster Recovery Mnemonic Derivation.
/// </summary>
public sealed class QrAuthenticationSecurityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public QrAuthenticationSecurityTests()
    {
        var connectionString = $"Data Source=file:memdb_qrauth_{Guid.NewGuid():N}?mode=memory&cache=shared;Pooling=False;Default Timeout=30;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<DietTrackerDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        var jwtOptions = new JwtOptions
        {
            Issuer = "DietDostTestIssuer",
            Audience = "DietDostTestAudience",
            Key = "DietDost_Super_Secret_Test_Key_For_QrAuth_HMAC_SHA256_Authentication_2026_Min32Bytes!",
            ExpiryMinutes = 60
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Register application CQRS handlers and native dispatcher
        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AnonymousRegistration_Succeeds_AndGenerates_ValidBip39RecoveryKit_AndHashesSecret()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var cmd = new RegisterAnonymousCommand("Shiva Anonymous", AcceptTerms: true, AcceptHealthConsent: true);
        var result = await dispatcher.SendAsync(cmd);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Data);

        var data = result.Data!;
        Assert.NotEmpty(data.UserId);
        Assert.NotEmpty(data.Token);
        Assert.Equal(UserRole.User, data.User.Role);
        Assert.Equal(UserTier.Free, data.User.Tier);

        // Verify recovery kit
        Assert.Equal(24, data.RecoveryLookupId.Length);
        Assert.True(Bip39Mnemonic.Validate(data.MnemonicPhrase));
        Assert.Equal(12, data.MnemonicPhrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        // Verify in DB
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        var user = await db.Users.FindAsync(data.UserId);
        Assert.NotNull(user);
        Assert.True(user!.IsAnonymous);
        Assert.Equal("Shiva Anonymous", user.DisplayName);
        Assert.Equal(data.RecoveryLookupId, user.RecoveryLookupId);
        Assert.NotEmpty(user.RecoveryKeyHash!);
        Assert.NotEmpty(user.RecoverySalt!);
        Assert.NotEqual(data.RecoveryKeyHex, user.RecoveryKeyHash); // Zero-knowledge storage
    }

    [Fact]
    public async Task AnonymousRegistration_Fails_WhenConsentIsMissing()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var cmdWithoutTerms = new RegisterAnonymousCommand("Shiva Anonymous", AcceptTerms: false, AcceptHealthConsent: true);
        var result1 = await dispatcher.SendAsync(cmdWithoutTerms);
        Assert.False(result1.Succeeded);
        Assert.Equal(400, result1.StatusCode);

        var cmdWithoutConsent = new RegisterAnonymousCommand("Shiva Anonymous", AcceptTerms: true, AcceptHealthConsent: false);
        var result2 = await dispatcher.SendAsync(cmdWithoutConsent);
        Assert.False(result2.Succeeded);
        Assert.Equal(400, result2.StatusCode);
    }

    [Fact]
    public async Task InitiateQrSession_Generates_ValidSession_AndChallengeNonce()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var browserSecret = "test-browser-secret-token-12345678901234567890";
        var browserSecretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(browserSecret)));

        var cmd = new InitiateQrSessionCommand(browserSecretHash, "desktop-pubkey-sample", "127.0.0.1", "Mozilla/5.0 Test");
        var result = await dispatcher.SendAsync(cmd);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!.SessionId);
        Assert.NotEmpty(result.Data.ChallengeNonce);
        Assert.Equal(2, result.Data.ConfirmationCode.Length);
        Assert.NotEmpty(result.Data.ConfirmationGlyph);
        Assert.StartsWith("dietdost://auth/qr?", result.Data.QrPayloadUri);
    }

    [Fact]
    public async Task CompleteQrFlow_WithDualTokenMemorySecret_DefeatsShoulderSurfing()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // 1. Create a user (mobile user who will approve)
        var regCmd = new RegisterAnonymousCommand("Mobile Approver", AcceptTerms: true, AcceptHealthConsent: true);
        var regResult = await dispatcher.SendAsync(regCmd);
        var approverUserId = regResult.Data!.UserId;

        // 2. Desktop initiates QR session
        var legitimateBrowserSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var secretHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legitimateBrowserSecret)));

        var initCmd = new InitiateQrSessionCommand(secretHash, null, "192.168.1.50", "Desktop Chrome");
        var initResult = await dispatcher.SendAsync(initCmd);
        var sessionId = initResult.Data!.SessionId;
        var challengeNonce = initResult.Data.ChallengeNonce;

        // 3. Status is Pending
        var statusQuery = new GetQrSessionStatusQuery(sessionId);
        var statusResult1 = await dispatcher.QueryAsync(statusQuery);
        Assert.Equal(QrSessionStatus.Pending, statusResult1.Data!.Status);

        // 4. Mobile scans and approves the session
        var approveCmd = new ApproveQrSessionCommand(
            SessionId: sessionId,
            ChallengeNonce: challengeNonce,
            UserId: approverUserId,
            MobilePublicKey: "mobile-p256-key",
            EncryptedApprovalPayload: "enc-payload");
        var approveResult = await dispatcher.SendAsync(approveCmd);
        Assert.True(approveResult.Succeeded, approveResult.Error);

        // 5. Desktop polls status -> Approved, receives ExchangeTicket
        var statusResult2 = await dispatcher.QueryAsync(statusQuery);
        Assert.Equal(QrSessionStatus.Approved, statusResult2.Data!.Status);
        var exchangeTicket = statusResult2.Data.ExchangeTicket;
        Assert.NotNull(exchangeTicket);

        // 6. ADVERSARIAL TEST: Attacker intercepted QR code and ExchangeTicket, but tries with WRONG browser secret
        var attackerWrongSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var attackerExchangeCmd = new ExchangeQrTicketCommand(sessionId, exchangeTicket!, attackerWrongSecret);
        var attackerResult = await dispatcher.SendAsync(attackerExchangeCmd);

        Assert.False(attackerResult.Succeeded);
        Assert.Equal(401, attackerResult.StatusCode);
        Assert.Equal("BrowserSecretMismatch", attackerResult.ErrorCode);

        // 7. LEGITIMATE TEST: Browser exchanges with genuine memory secret
        var legitExchangeCmd = new ExchangeQrTicketCommand(sessionId, exchangeTicket!, legitimateBrowserSecret);
        var legitResult = await dispatcher.SendAsync(legitExchangeCmd);

        Assert.True(legitResult.Succeeded, legitResult.Error);
        Assert.NotNull(legitResult.Data);
        Assert.Equal(approverUserId, legitResult.Data!.User.Id);
        Assert.NotEmpty(legitResult.Data.Token);

        // 8. REPLAY PREVENTION TEST: Attempt to exchange the ticket a second time
        var replayResult = await dispatcher.SendAsync(legitExchangeCmd);
        Assert.False(replayResult.Succeeded);
        Assert.Equal(409, replayResult.StatusCode);
        Assert.Equal("TicketAlreadyConsumed", replayResult.ErrorCode);
    }

    [Fact]
    public async Task AccountRecovery_Succeeds_ViaMnemonic_AndDerivesSameRecoveryLookupId()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // 1. Register anonymous user and obtain 12-word mnemonic
        var regCmd = new RegisterAnonymousCommand("Disaster Recovery Persona", AcceptTerms: true, AcceptHealthConsent: true);
        var regResult = await dispatcher.SendAsync(regCmd);
        var originalUserId = regResult.Data!.UserId;
        var mnemonic = regResult.Data.MnemonicPhrase;
        var originalLookupId = regResult.Data.RecoveryLookupId;

        // 2. Perform emergency account recovery using mnemonic
        var recoverCmd = new RecoverAccountCommand(mnemonic);
        var recoverResult = await dispatcher.SendAsync(recoverCmd);

        Assert.True(recoverResult.Succeeded, recoverResult.Error);
        Assert.NotNull(recoverResult.Data);
        Assert.Equal(originalUserId, recoverResult.Data!.User.Id);
        Assert.Equal(24, recoverResult.Data.NewRecoveryLookupId.Length);
        Assert.NotEqual(originalLookupId, recoverResult.Data.NewRecoveryLookupId); // Forward secrecy key rotation
        Assert.NotEmpty(recoverResult.Data.Token);

        // 3. Adversarial test: Corrupted mnemonic (tampered 12th word) fails validation
        var words = mnemonic.Split(' ').ToArray();
        words[11] = words[11] == "abandon" ? "ability" : "abandon"; // Alter checksum word
        var tamperedMnemonic = string.Join(' ', words);

        var badRecoverCmd = new RecoverAccountCommand(tamperedMnemonic);
        var badResult = await dispatcher.SendAsync(badRecoverCmd);
        Assert.False(badResult.Succeeded);
        Assert.Equal(401, badResult.StatusCode);
        Assert.Equal("InvalidMnemonic", badResult.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedUser_CannotApproveQrSession()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        // 1. Register user
        var regCmd = new RegisterAnonymousCommand("To Be Deleted", AcceptTerms: true, AcceptHealthConsent: true);
        var regResult = await dispatcher.SendAsync(regCmd);
        var userId = regResult.Data!.UserId;

        // 2. Soft-delete user
        var user = await db.Users.FindAsync(userId);
        user!.IsDeleted = true;
        user.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // 3. Initiate QR
        var secret = "test-secret";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var initResult = await dispatcher.SendAsync(new InitiateQrSessionCommand(hash));

        // 4. Deleted user attempts to approve
        var approveCmd = new ApproveQrSessionCommand(
            initResult.Data!.SessionId,
            initResult.Data.ChallengeNonce,
            userId);
        var approveResult = await dispatcher.SendAsync(approveCmd);

        Assert.False(approveResult.Succeeded);
        Assert.Equal(403, approveResult.StatusCode);
        Assert.Equal("UserInactive", approveResult.ErrorCode);
    }
}

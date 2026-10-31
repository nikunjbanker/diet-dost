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
using Nutrition.Application.Features.Auth.Commands.RejectQrSession;
using Nutrition.Application.Features.Auth.Commands.SimulateQrApproval;
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
        Assert.Contains("/qr-approve.html?", result.Data.QrPayloadUri);
        Assert.StartsWith("dietdost://auth/qr?", result.Data.DeepLinkUri);
        Assert.NotEmpty(result.Data.QrSvg!);
        Assert.StartsWith("<svg", result.Data.QrSvg);
        Assert.NotEmpty(result.Data.DataUri!);
        Assert.StartsWith("data:image/svg+xml;base64,", result.Data.DataUri);
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
        Assert.Equal(originalLookupId, recoverResult.Data.NewRecoveryLookupId); // Standard sign-in preserves credentials
        Assert.NotEmpty(recoverResult.Data.Token);

        // 3. Adversarial test: Corrupted mnemonic (tampered 12th word checksum bit) fails validation
        var words = mnemonic.Split(' ').ToArray();
        int lastIdx = Array.IndexOf(Bip39EnglishWordList.Words, words[11]);
        words[11] = Bip39EnglishWordList.Words[lastIdx ^ 1]; // Flips checksum bit while keeping entropy identical
        var tamperedMnemonic = string.Join(' ', words);

        var badRecoverCmd = new RecoverAccountCommand(tamperedMnemonic);
        var badResult = await dispatcher.SendAsync(badRecoverCmd);
        Assert.False(badResult.Succeeded);
        Assert.Equal(401, badResult.StatusCode);
        Assert.Equal("InvalidMnemonic", badResult.ErrorCode);
    }

    [Fact]
    public async Task MnemonicAndJsonLogin_CanLoginRepeatedlyWithoutCredentialInvalidation()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // 1. Register anonymous user
        var regCmd = new RegisterAnonymousCommand("Multi Login Persona", AcceptTerms: true, AcceptHealthConsent: true);
        var regResult = await dispatcher.SendAsync(regCmd);
        Assert.True(regResult.Succeeded);
        var userId = regResult.Data!.UserId;
        var mnemonic = regResult.Data.MnemonicPhrase;
        var recoveryKey = regResult.Data.RecoveryKeyHex;
        var lookupId = regResult.Data.RecoveryLookupId;
        var jsonBackup = regResult.Data.JsonBackupPayload;

        // 2. First login with 12-word mnemonic
        var login1 = await dispatcher.SendAsync(new RecoverAccountCommand(mnemonic));
        Assert.True(login1.Succeeded);
        Assert.Equal(userId, login1.Data!.User.Id);
        Assert.Equal(lookupId, login1.Data.NewRecoveryLookupId);

        // 3. Second login with exact same 12-word mnemonic (MUST SUCCEED - credentials preserved)
        var login2 = await dispatcher.SendAsync(new RecoverAccountCommand(mnemonic));
        Assert.True(login2.Succeeded);
        Assert.Equal(userId, login2.Data!.User.Id);
        Assert.Equal(lookupId, login2.Data.NewRecoveryLookupId);

        // 4. Third login with recovery key hex (MUST SUCCEED)
        var login3 = await dispatcher.SendAsync(new RecoverAccountCommand(recoveryKey));
        Assert.True(login3.Succeeded);
        Assert.Equal(userId, login3.Data!.User.Id);

        // 5. Explicit Key Rotation: rotateKey = true
        var rotateLogin = await dispatcher.SendAsync(new RecoverAccountCommand(mnemonic, RotateKey: true));
        Assert.True(rotateLogin.Succeeded);
        Assert.NotEqual(lookupId, rotateLogin.Data!.NewRecoveryLookupId);
        var newMnemonic = rotateLogin.Data.NewMnemonicPhrase;

        // 6. Old mnemonic should now fail (401)
        var oldFailLogin = await dispatcher.SendAsync(new RecoverAccountCommand(mnemonic));
        Assert.False(oldFailLogin.Succeeded);
        Assert.Equal(401, oldFailLogin.StatusCode);

        // 7. New mnemonic succeeds
        var newSuccessLogin = await dispatcher.SendAsync(new RecoverAccountCommand(newMnemonic));
        Assert.True(newSuccessLogin.Succeeded);
        Assert.Equal(userId, newSuccessLogin.Data!.User.Id);
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

    [Fact]
    public async Task SimulateQrApproval_Succeeds_AndApproves_PendingSession()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // 1. Seed demo user
        var regCmd = new RegisterAnonymousCommand("Demo User", AcceptTerms: true, AcceptHealthConsent: true);
        var regResult = await dispatcher.SendAsync(regCmd);
        Assert.True(regResult.Succeeded);

        // 2. Initiate QR
        var secret = "demo-secret-1234567890123456789012345";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var initResult = await dispatcher.SendAsync(new InitiateQrSessionCommand(hash));
        Assert.True(initResult.Succeeded);

        var sessionId = initResult.Data!.SessionId;
        var challengeNonce = initResult.Data.ChallengeNonce;

        // 3. Simulate approval
        var simCmd = new SimulateQrApprovalCommand(sessionId, challengeNonce, DemoEmail: "basic@dietdost.app");
        var simResult = await dispatcher.SendAsync(simCmd);

        Assert.True(simResult.Succeeded, simResult.Error);
        Assert.NotEmpty(simResult.Data!);

        // 4. Verify session transitioned to Approved
        var status = await dispatcher.QueryAsync(new GetQrSessionStatusQuery(sessionId));
        Assert.Equal(QrSessionStatus.Approved, status.Data!.Status);
        Assert.Equal(simResult.Data, status.Data.ExchangeTicket);
    }

    [Fact]
    public async Task RejectQrSession_TransitionsTo_RejectedStatus()
    {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // 1. Initiate QR
        var secret = "demo-secret-reject-123456789012345";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var initResult = await dispatcher.SendAsync(new InitiateQrSessionCommand(hash));
        Assert.True(initResult.Succeeded);

        var sessionId = initResult.Data!.SessionId;
        var challengeNonce = initResult.Data.ChallengeNonce;

        // 2. Reject QR session
        var rejectCmd = new RejectQrSessionCommand(sessionId, challengeNonce);
        var rejectResult = await dispatcher.SendAsync(rejectCmd);

        Assert.True(rejectResult.Succeeded, rejectResult.Error);

        // 3. Verify session transitioned to Rejected
        var status = await dispatcher.QueryAsync(new GetQrSessionStatusQuery(sessionId));
        Assert.Equal(QrSessionStatus.Rejected, status.Data!.Status);
    }
}


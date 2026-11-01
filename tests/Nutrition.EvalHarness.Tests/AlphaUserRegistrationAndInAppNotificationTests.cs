/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Features.Admin.Commands.UserManagement;
using Nutrition.Application.Features.Auth.Commands.Login;
using Nutrition.Application.Features.Auth.Commands.RegisterUser;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.Infrastructure.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Comprehensive TDD test suite validating Alpha State User Registration Gating,
/// SuperAdmin Approval Workflow, and In-App Persistent Notifications.
/// </summary>
public class AlphaUserRegistrationAndInAppNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly EfRepository<ApplicationUser> _userRepo;
    private readonly EfRepository<UserProfile> _profileRepo;
    private readonly EfRepository<VerificationOtp> _otpRepo;
    private readonly EfRepository<TierFeatureConfiguration> _tierRepo;
    private readonly EfRepository<AppNotification> _notifRepo;
    private readonly EfUnitOfWork _uow;
    private readonly Pbkdf2PasswordHasher _passwordHasher;
    private readonly OtpService _otpService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAppEnvironment _appEnvironment;
    private readonly TestInAppNotificationService _testNotificationService;
    private readonly IOptions<AuthOptions> _authOptions;

    public AlphaUserRegistrationAndInAppNotificationTests()
    {
        TierConfigurationService.ClearCache();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        _userRepo = new EfRepository<ApplicationUser>(_db, NullLogger<EfRepository<ApplicationUser>>.Instance);
        _profileRepo = new EfRepository<UserProfile>(_db, NullLogger<EfRepository<UserProfile>>.Instance);
        _otpRepo = new EfRepository<VerificationOtp>(_db, NullLogger<EfRepository<VerificationOtp>>.Instance);
        _tierRepo = new EfRepository<TierFeatureConfiguration>(_db, NullLogger<EfRepository<TierFeatureConfiguration>>.Instance);
        _notifRepo = new EfRepository<AppNotification>(_db, NullLogger<EfRepository<AppNotification>>.Instance);
        _uow = new EfUnitOfWork(_db, NullLogger<EfUnitOfWork>.Instance);

        _passwordHasher = new Pbkdf2PasswordHasher();
        _otpService = new OtpService();
        _appEnvironment = new TestAppEnvironment(isDebug: true, isDevelopment: true);
        _testNotificationService = new TestInAppNotificationService(_db);

        _authOptions = Options.Create(new AuthOptions
        {
            AllowRegistration = true,
            SuperAdminEmail = "nikunj_banker@hotmail.com",
            TermsVersion = "v1.0-202609",
            HealthConsentVersion = "v1.0-202609"
        });

        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "TestSecretKeyForJwtAuthenticationInEvalHarness2026!LongEnough32Bytes",
            Issuer = "DietDostTest",
            Audience = "DietDostTestClient",
            ExpiryMinutes = 60
        });
        _jwtTokenService = new JwtTokenService(jwtOptions);
    }

    public void Dispose()
    {
        TierConfigurationService.ClearCache();
        _connection.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RegisterUser_InAlphaState_CreatesInactiveAccount_AndPersistsSuperAdminNotification()
    {
        var registerHandler = new RegisterUserCommandHandler(
            _userRepo,
            _profileRepo,
            _otpRepo,
            _uow,
            _passwordHasher,
            _otpService,
            _authOptions,
            _testNotificationService,
            NullLogger<RegisterUserCommandHandler>.Instance);

        var command = new RegisterUserCommand(
            Name: "Alpha Tester",
            Email: "alphatester@example.com",
            MobileNumber: "+919876543210",
            Password: "SecurePassword@2026!",
            AcceptTerms: true,
            AcceptHealthConsent: true,
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0 Test",
            IsDev: true
        );

        var result = await registerHandler.HandleAsync(command);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.RequiresOwnerApproval, "Registration must flag that owner approval is required during Alpha.");

        // Assert user exists in database with IsActive = false (Pending approval)
        var userInDb = await _db.Users.FirstOrDefaultAsync(u => u.Email == "alphatester@example.com");
        Assert.NotNull(userInDb);
        Assert.False(userInDb.IsActive, "New Alpha accounts must be created with IsActive = false.");

        // Assert in-app notification is recorded for SuperAdmin
        var notif = await _db.Set<AppNotification>().FirstOrDefaultAsync(n => n.Type == "NewRegistration");
        Assert.NotNull(notif);
        Assert.Equal("SuperAdmin", notif.RecipientRole);
        Assert.Contains("alphatester@example.com", notif.Message);
        Assert.Contains("Alpha Tester", notif.Message);
        Assert.False(notif.IsRead);
    }

    [Fact]
    public async Task Login_WithUnapprovedAccount_ReturnsForbiddenWithPendingApprovalMessage()
    {
        // Arrange: Seed an unapproved user (IsActive = false)
        var user = new ApplicationUser
        {
            Id = "user-unapproved-1",
            Email = "unapproved@example.com",
            NormalizedEmail = "UNAPPROVED@EXAMPLE.COM",
            MobileNumber = "+919876543211",
            NormalizedMobileNumber = "919876543211",
            PasswordHash = _passwordHasher.HashPassword("SecurePassword@2026!"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsActive = false,
            IsEmailVerified = false,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        var loginHandler = new LoginCommandHandler(
            _userRepo,
            _profileRepo,
            _tierRepo,
            _uow,
            _passwordHasher,
            _jwtTokenService,
            _appEnvironment,
            NullLogger<LoginCommandHandler>.Instance);

        var loginCommand = new LoginCommand("unapproved@example.com", "SecurePassword@2026!");
        var result = await loginHandler.HandleAsync(loginCommand);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("AccountPendingApproval", result.ErrorCode);
        Assert.Contains("pending review and activation by the application owner", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuperAdmin_ActivatingUser_EnablesLogin_AndPersistsActivationNotification()
    {
        // 1. Arrange: Unapproved user
        var user = new ApplicationUser
        {
            Id = "user-to-activate-1",
            Email = "pending.user@example.com",
            NormalizedEmail = "PENDING.USER@EXAMPLE.COM",
            MobileNumber = "+919876543212",
            NormalizedMobileNumber = "919876543212",
            PasswordHash = _passwordHasher.HashPassword("SecurePassword@2026!"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsActive = false,
            IsEmailVerified = false,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        // 2. SuperAdmin activates user
        var statusHandler = new UpdateUserStatusCommandHandler(
            _userRepo,
            _uow,
            _testNotificationService,
            NullLogger<UpdateUserStatusCommandHandler>.Instance);

        var updateCommand = new UpdateUserStatusCommand(
            TargetUserId: user.Id,
            IsActive: true,
            AdminUserId: "superadmin-root"
        );

        var updateResult = await statusHandler.HandleAsync(updateCommand);
        Assert.True(updateResult.Succeeded, updateResult.Error);

        var refreshedUser = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(refreshedUser);
        Assert.True(refreshedUser.IsActive);

        // 3. Assert activation notification recorded for applicant
        var activationNotif = await _db.Set<AppNotification>()
            .FirstOrDefaultAsync(n => n.RecipientUserId == user.Id && n.Type == "AccountActivated");
        Assert.NotNull(activationNotif);
        Assert.Contains("activated", activationNotif.Message, StringComparison.OrdinalIgnoreCase);

        // 4. Assert user can now log in successfully
        var loginHandler = new LoginCommandHandler(
            _userRepo,
            _profileRepo,
            _tierRepo,
            _uow,
            _passwordHasher,
            _jwtTokenService,
            _appEnvironment,
            NullLogger<LoginCommandHandler>.Instance);

        var loginResult = await loginHandler.HandleAsync(new LoginCommand("pending.user@example.com", "SecurePassword@2026!"));
        Assert.True(loginResult.Succeeded, loginResult.Error);
        Assert.NotNull(loginResult.Data?.Token);
    }

    [Fact]
    public async Task SeededDemoAccounts_RemainPreActivated_AndCanLoginWithoutApproval()
    {
        // Seed demo accounts
        var demoAccounts = new[]
        {
            ("free@dietdost.app", UserTier.Free, UserRole.User),
            ("basic@dietdost.app", UserTier.Basic, UserRole.User),
            ("premium@dietdost.app", UserTier.Premium, UserRole.User),
            ("admin.demo@dietdost.app", UserTier.Premium, UserRole.Admin),
            ("superadmin@dietdost.app", UserTier.SuperAdmin, UserRole.SuperAdmin),
            ("nikunj_banker@hotmail.com", UserTier.SuperAdmin, UserRole.SuperAdmin)
        };

        foreach (var (email, tier, role) in demoAccounts)
        {
            var user = new ApplicationUser
            {
                Id = $"demo-{Guid.NewGuid():N}",
                Email = email,
                NormalizedEmail = ApplicationUser.NormalizeEmailAddress(email),
                MobileNumber = "+919999999999",
                NormalizedMobileNumber = "919999999999",
                PasswordHash = _passwordHasher.HashPassword("DietDost@Demo2026!"),
                Role = role,
                Tier = tier,
                IsActive = true,
                IsEmailVerified = true,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = "v1.0-202609"
            };
            await _db.Users.AddAsync(user);
        }
        await _db.SaveChangesAsync();

        var loginHandler = new LoginCommandHandler(
            _userRepo,
            _profileRepo,
            _tierRepo,
            _uow,
            _passwordHasher,
            _jwtTokenService,
            _appEnvironment,
            NullLogger<LoginCommandHandler>.Instance);

        foreach (var (email, _, _) in demoAccounts)
        {
            var result = await loginHandler.HandleAsync(new LoginCommand(email, "DietDost@Demo2026!"));
            Assert.True(result.Succeeded, $"Demo account {email} should log in without approval: {result.Error}");
        }
    }

    /// <summary>
    /// Test spy implementation of IInAppNotificationService that writes directly to DbContext for unit tests.
    /// </summary>
    private class TestInAppNotificationService : IInAppNotificationService
    {
        private readonly DietTrackerDbContext _db;
        public List<AppNotification> DispatchedNotifications { get; } = new();

        public TestInAppNotificationService(DietTrackerDbContext db)
        {
            _db = db;
        }

        public async Task NotifySuperAdminNewRegistrationAsync(
            string userId, string name, string email, string mobile, DateTime createdAtUtc, CancellationToken ct = default)
        {
            var notif = new AppNotification
            {
                Id = Guid.NewGuid().ToString("N"),
                RecipientRole = "SuperAdmin",
                Title = "New Alpha User Registration",
                Message = $"Applicant {name} ({email}, {mobile}) created an account awaiting approval.",
                Type = "NewRegistration",
                MetadataJson = $"{{\"userId\":\"{userId}\",\"email\":\"{email}\",\"name\":\"{name}\"}}",
                IsRead = false,
                CreatedAtUtc = createdAtUtc
            };
            DispatchedNotifications.Add(notif);
            await _db.Set<AppNotification>().AddAsync(notif, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task NotifyUserAccountActivatedAsync(string userId, string email, CancellationToken ct = default)
        {
            var notif = new AppNotification
            {
                Id = Guid.NewGuid().ToString("N"),
                RecipientUserId = userId,
                Title = "Diet-Dost Account Activated",
                Message = $"Your Diet-Dost account ({email}) has been activated by the application owner. You can now log in.",
                Type = "AccountActivated",
                MetadataJson = $"{{\"userId\":\"{userId}\",\"email\":\"{email}\"}}",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            DispatchedNotifications.Add(notif);
            await _db.Set<AppNotification>().AddAsync(notif, ct);
            await _db.SaveChangesAsync(ct);
        }
    }

    private sealed class TestAppEnvironment(bool isDebug, bool isDevelopment) : IAppEnvironment
    {
        public bool IsDebugMode => isDebug;
        public bool IsDevelopment => isDevelopment;
        public bool AllowsDemoUsers => true;
        public bool AllowsAdminDemoUsers => true;
    }
}

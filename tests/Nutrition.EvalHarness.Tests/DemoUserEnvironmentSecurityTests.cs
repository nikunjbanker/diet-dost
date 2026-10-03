/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Features.Auth.Commands.Login;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DemoUserEnvironmentSecurityTests
{
    private class FakeAppEnvironment : IAppEnvironment
    {
        public bool IsDebugMode { get; set; }
        public bool IsDevelopment { get; set; }
        public bool? ExplicitAllowsDemoUsers { get; set; }

        public bool AllowsDemoUsers => ExplicitAllowsDemoUsers ?? (IsDebugMode && IsDevelopment);
        public bool AllowsAdminDemoUsers => IsDebugMode && IsDevelopment;
    }

    private class InMemoryRepo<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items = new();

        public InMemoryRepo(IEnumerable<T>? seed = null)
        {
            if (seed != null) _items.AddRange(seed);
        }

        public Task<T?> GetByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => (x as ApplicationUser)?.Id == id || (x as UserProfile)?.Id == id));

        public Task<List<T>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(_items.ToList());

        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().Where(predicate).ToList());

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().Any(predicate));

        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_items.AsQueryable().Count(predicate));

        public IQueryable<T> Query() => _items.AsQueryable();

        public Task AddAsync(T entity, CancellationToken ct = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var item = _items.FirstOrDefault(x => (x as ApplicationUser)?.Id == id || (x as UserProfile)?.Id == id);
            if (item != null) _items.Remove(item);
            return Task.CompletedTask;
        }
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => "HASH_" + password;
        public bool VerifyPassword(string password, string hash) => hash == "HASH_" + password;
    }

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(ApplicationUser user, string? displayName = null) => "fake_jwt_token";
        public ClaimsPrincipal? ValidateToken(string token) => new ClaimsPrincipal();
    }

    [Theory]
    [InlineData("free@dietdost.app", true)]
    [InlineData("basic@dietdost.app", true)]
    [InlineData("premium@dietdost.app", true)]
    [InlineData("admin.demo@dietdost.app", true)]
    [InlineData("superadmin@dietdost.app", true)]
    [InlineData("admin@dietdost.app", true)]
    [InlineData("realuser@example.com", false)]
    [InlineData("patient@apollo.hospital", false)]
    public void ApplicationUser_CorrectlyIdentifies_DemoEmails(string email, bool expectedIsDemo)
    {
        var user = new ApplicationUser
        {
            Email = email,
            NormalizedEmail = ApplicationUser.NormalizeEmailAddress(email)
        };

        Assert.Equal(expectedIsDemo, user.IsDemoAccount);
        Assert.Equal(expectedIsDemo, ApplicationUser.IsDemoEmail(email));
    }

    [Fact]
    public async Task Login_InReleaseOrProductionMode_StrictlyBlocks_DemoUser()
    {
        // Arrange: Environment configured as Release / Production (AllowsDemoUsers = false)
        var releaseEnv = new FakeAppEnvironment
        {
            IsDebugMode = false,
            IsDevelopment = false
        };

        var demoUser = new ApplicationUser
        {
            Id = "user-free",
            Email = "free@dietdost.app",
            NormalizedEmail = "FREE@DIETDOST.APP",
            PasswordHash = "HASH_DietDost@Demo2026!",
            IsEmailVerified = true,
            IsActive = true,
            Tier = UserTier.Free,
            Role = UserRole.User
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { demoUser });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var jwt = new FakeJwtTokenService();
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(userRepo, profileRepo, tierRepo, uow, hasher, jwt, releaseEnv, logger);

        // Act: Attempt to login as demo user in Release mode
        var result = await handler.HandleAsync(new LoginCommand("free@dietdost.app", "DietDost@Demo2026!"));

        // Assert: Access must be strictly forbidden to avoid data breach
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("DemoAccessForbidden", result.ErrorCode);
        Assert.Contains("Release mode", result.Error);
    }

    [Fact]
    public async Task Login_InDebugAndDevelopmentMode_Allows_DemoUser()
    {
        // Arrange: Environment configured as Debug + Development (AllowsDemoUsers = true)
        var debugEnv = new FakeAppEnvironment
        {
            IsDebugMode = true,
            IsDevelopment = true
        };

        var demoUser = new ApplicationUser
        {
            Id = "user-premium",
            Email = "premium@dietdost.app",
            NormalizedEmail = "PREMIUM@DIETDOST.APP",
            PasswordHash = "HASH_DietDost@Demo2026!",
            IsEmailVerified = true,
            IsActive = true,
            Tier = UserTier.Premium,
            Role = UserRole.User
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { demoUser });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { new UserProfile { Id = "user-premium", Name = "Premium User" } });
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var jwt = new FakeJwtTokenService();
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(userRepo, profileRepo, tierRepo, uow, hasher, jwt, debugEnv, logger);

        // Act: Attempt to login as demo user in Debug mode
        var result = await handler.HandleAsync(new LoginCommand("premium@dietdost.app", "DietDost@Demo2026!"));

        // Assert: Login must succeed
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("fake_jwt_token", result.Data.Token);
        Assert.Equal(UserTier.Premium, result.Data.User.Tier);
    }

    [Fact]
    public async Task Login_InReleaseMode_Allows_RealNonDemoUser()
    {
        // Arrange: Environment configured as Release (AllowsDemoUsers = false)
        var releaseEnv = new FakeAppEnvironment
        {
            IsDebugMode = false,
            IsDevelopment = false
        };

        var realUser = new ApplicationUser
        {
            Id = "real-user-123",
            Email = "doctor.sharma@hospital.org",
            NormalizedEmail = "DOCTOR.SHARMA@HOSPITAL.ORG",
            PasswordHash = "HASH_StrongPassword2026!",
            IsEmailVerified = true,
            IsActive = true,
            Tier = UserTier.Premium,
            Role = UserRole.User
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { realUser });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { new UserProfile { Id = "real-user-123", Name = "Dr. Sharma" } });
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var jwt = new FakeJwtTokenService();
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(userRepo, profileRepo, tierRepo, uow, hasher, jwt, releaseEnv, logger);

        // Act: Attempt to login as real production user in Release mode
        var result = await handler.HandleAsync(new LoginCommand("doctor.sharma@hospital.org", "StrongPassword2026!"));

        // Assert: Legitimate non-demo user can login without hindrance
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("doctor.sharma@hospital.org", result.Data.User.Email);
    }

    [Theory]
    [InlineData("admin.demo@dietdost.app", true)]
    [InlineData("superadmin@dietdost.app", true)]
    [InlineData("admin@dietdost.app", true)]
    [InlineData("free@dietdost.app", false)]
    [InlineData("basic@dietdost.app", false)]
    [InlineData("premium@dietdost.app", false)]
    [InlineData("doctor@hospital.org", false)]
    public void ApplicationUser_CorrectlyIdentifies_PrivilegedDemoEmails(string email, bool expectedIsPrivileged)
    {
        var user = new ApplicationUser
        {
            Email = email,
            NormalizedEmail = ApplicationUser.NormalizeEmailAddress(email)
        };

        Assert.Equal(expectedIsPrivileged, user.IsPrivilegedDemoAccount);
        Assert.Equal(expectedIsPrivileged, ApplicationUser.IsPrivilegedDemoEmail(email));
    }

    [Theory]
    [InlineData("free@dietdost.app", UserTier.Free)]
    [InlineData("basic@dietdost.app", UserTier.Basic)]
    [InlineData("premium@dietdost.app", UserTier.Premium)]
    public async Task Login_InReleaseProduction_WithShowcaseAllowDemoUsers_PermitsEndUserDemoAccounts(string email, UserTier tier)
    {
        // Arrange: Environment configured as Release/Production with Security:AllowDemoUsers=true
        var showcaseEnv = new FakeAppEnvironment
        {
            IsDebugMode = false,
            IsDevelopment = false,
            ExplicitAllowsDemoUsers = true
        };

        var demoUser = new ApplicationUser
        {
            Id = $"user-{tier.ToString().ToLower()}",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "HASH_DietDost@Demo2026!",
            IsEmailVerified = true,
            IsActive = true,
            Tier = tier,
            Role = UserRole.User
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { demoUser });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { new UserProfile { Id = demoUser.Id, Name = $"{tier} User" } });
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var jwt = new FakeJwtTokenService();
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(userRepo, profileRepo, tierRepo, uow, hasher, jwt, showcaseEnv, logger);

        // Act: Attempt to login as free/basic/premium demo user in showcase release mode
        var result = await handler.HandleAsync(new LoginCommand(email, "DietDost@Demo2026!"));

        // Assert: Login must succeed for end-user demo accounts in showcase
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(tier, result.Data.User.Tier);
    }

    [Theory]
    [InlineData("admin.demo@dietdost.app", UserRole.Admin)]
    [InlineData("superadmin@dietdost.app", UserRole.SuperAdmin)]
    [InlineData("admin@dietdost.app", UserRole.SuperAdmin)]
    public async Task Login_InReleaseProduction_WithShowcaseAllowDemoUsers_StrictlyBlocksAdminAndSuperAdminDemoAccounts(string email, UserRole role)
    {
        // Arrange: Environment configured as Release/Production with Security:AllowDemoUsers=true
        var showcaseEnv = new FakeAppEnvironment
        {
            IsDebugMode = false,
            IsDevelopment = false,
            ExplicitAllowsDemoUsers = true
        };

        var demoUser = new ApplicationUser
        {
            Id = role == UserRole.Admin ? "user-admin" : "user-superadmin",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "HASH_DietDost@Demo2026!",
            IsEmailVerified = true,
            IsActive = true,
            Tier = role == UserRole.Admin ? UserTier.Premium : UserTier.SuperAdmin,
            Role = role
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { demoUser });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { new UserProfile { Id = demoUser.Id, Name = $"{role} User" } });
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var jwt = new FakeJwtTokenService();
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(userRepo, profileRepo, tierRepo, uow, hasher, jwt, showcaseEnv, logger);

        // Act: Attempt to login as admin or superadmin demo user in showcase release mode
        var result = await handler.HandleAsync(new LoginCommand(email, "DietDost@Demo2026!"));

        // Assert: Login must be strictly blocked with 403 Forbidden
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("DemoAccessForbidden", result.ErrorCode);
        Assert.Contains("Admin and SuperAdmin demo accounts are strictly prohibited in released versions", result.Error);
    }

    [Fact]
    public void AppEnvironment_WithShowcaseConfiguration_AllowsDemoUsers_InReleaseProduction()
    {
        // Arrange: Production environment with explicit Security:AllowDemoUsers=true
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AllowDemoUsers"] = "true"
            })
            .Build();

        var hostEnv = new FakeHostEnvironment { EnvironmentName = "Production" };
        var appEnv = new Nutrition.Infrastructure.Services.AppEnvironment(hostEnv, config);

        // Assert: End-user demo users are permitted for the showcase deployment, but admin demo users are strictly prohibited
        Assert.False(appEnv.IsDevelopment);
        Assert.True(appEnv.AllowsDemoUsers);
        Assert.False(appEnv.AllowsAdminDemoUsers);
    }

    [Fact]
    public void AppEnvironment_WithoutShowcaseConfiguration_DeniesDemoUsers_InReleaseProduction()
    {
        // Arrange: Production environment without showcase toggle
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AllowDemoUsers"] = "false"
            })
            .Build();

        var hostEnv = new FakeHostEnvironment { EnvironmentName = "Production" };
        var appEnv = new Nutrition.Infrastructure.Services.AppEnvironment(hostEnv, config);

        // Assert: In standard production, demo users are strictly disallowed
        Assert.False(appEnv.IsDevelopment);
        Assert.False(appEnv.AllowsDemoUsers);
        Assert.False(appEnv.AllowsAdminDemoUsers);
    }

    private class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Nutrition.WebGateway";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

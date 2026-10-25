/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Features.Auth.Commands.Login;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Extensions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class SuperAdminBootstrapTests
{
    private class FakeAppEnvironment : IAppEnvironment
    {
        public bool IsDebugMode { get; set; }
        public bool IsDevelopment { get; set; }
        public bool? ExplicitAllowsDemoUsers { get; set; }

        public bool AllowsDemoUsers => ExplicitAllowsDemoUsers ?? (IsDebugMode && IsDevelopment);
        public bool AllowsAdminDemoUsers => IsDebugMode && IsDevelopment;
    }

    private class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => "HASH_" + password;
        public bool VerifyPassword(string password, string hash) => hash == "HASH_" + password;
    }

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(ApplicationUser user, string? displayName = null) => "fake_jwt_token_" + user.Role;
        public ClaimsPrincipal? ValidateToken(string token) => new ClaimsPrincipal();
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

        public Task<List<T>> FindDeletedAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        {
            var query = _items.AsQueryable();
            if (typeof(Nutrition.Domain.Common.ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                query = query.Where(x => ((Nutrition.Domain.Common.ISoftDeletable)x).IsDeleted);
            }
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            return Task.FromResult(query.ToList());
        }

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

    [Fact]
    public async Task Login_InReleaseProduction_WithConfiguredSuperAdmin_SuccessfullyLogsIn()
    {
        // Arrange: Production environment where demo users are disabled
        var prodEnv = new FakeAppEnvironment
        {
            IsDebugMode = false,
            IsDevelopment = false,
            ExplicitAllowsDemoUsers = false
        };

        var superAdmin = new ApplicationUser
        {
            Id = "superadmin-root",
            Email = "nikunj_banker@hotmail.com",
            NormalizedEmail = "NIKUNJ_BANKER@HOTMAIL.COM",
            PasswordHash = "HASH_MySecureSuperAdminPass2026!",
            IsEmailVerified = true,
            IsMobileVerified = true,
            IsActive = true,
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "v1.0-202609",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "v1.0-202609"
        };

        var profile = new UserProfile
        {
            Id = superAdmin.Id,
            Name = "Nikunj Banker (SuperAdmin)"
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { superAdmin });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { profile });
        var tierRepo = new InMemoryRepo<TierFeatureConfiguration>(TierFeatureConfiguration.GetDefaultConfigurations());
        var uow = new FakeUnitOfWork();
        var passwordHasher = new FakePasswordHasher();
        var jwtService = new FakeJwtTokenService();

        var handler = new LoginCommandHandler(
            userRepo,
            profileRepo,
            tierRepo,
            uow,
            passwordHasher,
            jwtService,
            prodEnv,
            NullLogger<LoginCommandHandler>.Instance
        );

        // Act
        var result = await handler.HandleAsync(new LoginCommand("nikunj_banker@hotmail.com", "MySecureSuperAdminPass2026!"));

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("nikunj_banker@hotmail.com", result.Data.User.Email);
        Assert.Equal(UserRole.SuperAdmin, result.Data.User.Role);
        Assert.Equal(UserTier.SuperAdmin, result.Data.User.Tier);
        Assert.StartsWith("fake_jwt_token_SuperAdmin", result.Data.Token);
    }

    [Fact]
    public void ConfigurationExtensions_ResolvesSuperAdminPassword_FromConfigurationSources()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Auth:SuperAdminEmail"] = "nikunj_banker@hotmail.com",
            ["SUPER_ADMIN_PASSWORD"] = "VaultSecretPass2026!"
        };

        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(inMemoryConfig);
        var mockEnv = new TestHostEnvironment { EnvironmentName = "Production" };

        Nutrition.WebGateway.Extensions.ConfigurationExtensions.AddDietDostAppConfiguration(builder, mockEnv);
        var config = builder.Build();

        Assert.Equal("nikunj_banker@hotmail.com", config["Auth:SuperAdminEmail"]);
        Assert.Equal("VaultSecretPass2026!", config["Auth:SuperAdminPassword"]);
    }

    [Fact]
    public void ApplicationUser_DistinguishesBetweenPrivilegedDemoAndProductionSuperAdmin()
    {
        Assert.True(ApplicationUser.IsPrivilegedDemoEmail("superadmin@dietdost.app"));
        Assert.True(ApplicationUser.IsPrivilegedDemoEmail("admin.demo@dietdost.app"));
        Assert.False(ApplicationUser.IsPrivilegedDemoEmail("nikunj_banker@hotmail.com"));

        var prodUser = new ApplicationUser
        {
            Id = "superadmin-root",
            Email = "nikunj_banker@hotmail.com",
            NormalizedEmail = "NIKUNJ_BANKER@HOTMAIL.COM"
        };

        Assert.False(prodUser.IsPrivilegedDemoAccount);
        Assert.False(prodUser.IsDemoAccount);
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Nutrition.WebGateway";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

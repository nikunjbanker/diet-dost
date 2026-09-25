using System.Linq.Expressions;
using System.Security.Claims;
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
}

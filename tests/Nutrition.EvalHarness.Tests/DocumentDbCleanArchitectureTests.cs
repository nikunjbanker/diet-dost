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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Features.Admin.Commands.UserManagement;
using Nutrition.Application.Features.Auth.Commands.DeleteAccount;
using Nutrition.Application.Features.Auth.Commands.VerifyOtp;
using Nutrition.Application.Features.Notifications.Commands.MarkNotificationRead;
using Nutrition.Application.Services;
using Nutrition.Domain.Common;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Services;
using Nutrition.Tools.DataMigrator.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DocumentDbCleanArchitectureTests
{
    [Fact]
    public async Task MarkNotificationReadCommand_BlocksCrossUserIdor_AndAllowsOwner()
    {
        var notif = new AppNotification
        {
            Id = "notif-idor-1",
            RecipientUserId = "owner-user",
            Title = "Test",
            Message = "Private",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var repo = new InMemoryRepo<AppNotification>(new[] { notif });
        var uow = new FakeUnitOfWork();
        var handler = new MarkNotificationReadCommandHandler(repo, uow);

        var idorResult = await handler.HandleAsync(new MarkNotificationReadCommand("notif-idor-1", "attacker-user", IsAdminOrSuper: false));
        Assert.False(idorResult.Succeeded);
        Assert.Equal(403, idorResult.StatusCode);
        Assert.False(notif.IsRead);

        var ownerResult = await handler.HandleAsync(new MarkNotificationReadCommand("notif-idor-1", "owner-user", IsAdminOrSuper: false));
        Assert.True(ownerResult.Succeeded);
        Assert.True(notif.IsRead);
        Assert.Equal(1, repo.UpdateCount);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task VerifyOtpCommand_RejectsInactiveUser()
    {
        var inactiveUser = new ApplicationUser
        {
            Id = "inactive-1",
            Email = "inactive@dietdost.app",
            NormalizedEmail = "INACTIVE@DIETDOST.APP",
            IsActive = false,
            IsDeleted = false
        };

        var handler = new VerifyOtpCommandHandler(
            new InMemoryRepo<ApplicationUser>(new[] { inactiveUser }),
            new InMemoryRepo<UserProfile>(),
            new InMemoryRepo<VerificationOtp>(),
            new InMemoryRepo<TierFeatureConfiguration>(),
            new FakeUnitOfWork(),
            new FakeOtpService(),
            new FakeJwtTokenService(),
            NullLogger<VerifyOtpCommandHandler>.Instance);

        var result = await handler.HandleAsync(new VerifyOtpCommand("inactive@dietdost.app", OtpChannel.Email, "123456"));
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task AdminUpdateUserCommand_BlocksNonSuperAdmin_FromAssigningSuperAdminTier()
    {
        var target = new ApplicationUser
        {
            Id = "user-target",
            Email = "user@dietdost.app",
            NormalizedEmail = "USER@DIETDOST.APP",
            Role = UserRole.User,
            Tier = UserTier.Basic,
            IsActive = true
        };

        var handler = new AdminUpdateUserCommandHandler(
            new InMemoryRepo<ApplicationUser>(new[] { target }),
            new InMemoryRepo<UserProfile>(),
            new FakeUnitOfWork(),
            new FakePasswordHasher(),
            new ConfigurationBuilder().Build(),
            NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var cmd = new AdminUpdateUserCommand(
            TargetUserId: "user-target",
            MobileNumber: null,
            Role: null,
            Tier: UserTier.SuperAdmin,
            IsActive: null,
            IsEmailVerified: null,
            NewPassword: null,
            AdminUserId: "admin-1",
            CurrentAdminRole: nameof(UserRole.Admin));

        var result = await handler.HandleAsync(cmd);
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void StorageInfrastructure_DocumentDb_FailsClosed_WithoutConnectionString_OrTls()
    {
        var missingConnConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "DocumentDb"
            })
            .Build();

        var services1 = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services1.AddStorageInfrastructure(missingConnConfig));

        var insecureRemoteConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "DocumentDb",
                ["ConnectionStrings:DocumentDbConnection"] = "mongodb://prod-cluster.mongocluster.cosmos.azure.com:10260/dietdost?tls=false"
            })
            .Build();

        var services2 = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services2.AddStorageInfrastructure(insecureRemoteConfig));

        var validLocalConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "DocumentDb",
                ["ConnectionStrings:DocumentDbConnection"] = "mongodb://localhost:27017/dietdost"
            })
            .Build();

        var services3 = new ServiceCollection();
        services3.AddStorageInfrastructure(validLocalConfig);
        Assert.DoesNotContain(services3, d => d.ServiceType == typeof(DietTrackerDbContext));
    }

    [Theory]
    [InlineData("Status", "Approved", 2)]
    [InlineData("Status", "Consumed", 4)]
    [InlineData("OperationType", "PhotoDetection", 0)]
    [InlineData("OperationType", "TextDetection", 1)]
    [InlineData("Channel", "Sms", 1)]
    [InlineData("Sex", "Female", 1)]
    [InlineData("ActivityLevel", "Moderate", 2)]
    [InlineData("DietaryPreference", "Vegan", 3)]
    public void DataConversionEngine_ResolvesExpandedDomainEnums(string columnName, string rawValue, int expectedInt)
    {
        var engine = new DataConversionEngine();
        var stats = new TableConversionStats { TableName = "Test" };
        var converted = engine.ConvertValue("Test", columnName, "INT", isNullable: false, maxLength: null, sourceValue: rawValue, rowKey: "1", stats: stats);
        Assert.Equal(expectedInt, converted);
    }

    [Fact]
    public async Task DeleteAccountCommand_ErasesPhysicalPhotoDirectory_AndPseudonymizesAiLogs()
    {
        var user = new ApplicationUser
        {
            Id = "user-dpdpa-1",
            Email = "dpdpa@dietdost.app",
            NormalizedEmail = "DPDPA@DIETDOST.APP",
            MobileNumber = "+919999999999",
            NormalizedMobileNumber = "919999999999",
            Role = UserRole.User,
            IsActive = true
        };
        var aiLog = new AiUsageLog { Id = "ai-1", UserId = "user-dpdpa-1", ModelId = "gemini-3-flash" };
        var photoStorage = new FakePhotoStorageService();

        var handler = new DeleteAccountCommandHandler(
            new InMemoryRepo<ApplicationUser>(new[] { user }),
            new InMemoryRepo<UserProfile>(),
            new InMemoryRepo<MealLog>(),
            new InMemoryRepo<DailyCalorieLedger>(),
            new InMemoryRepo<ProgressPhoto>(),
            new InMemoryRepo<AiDetectionFeedbackRecord>(),
            new InMemoryRepo<UserCorrectionRecord>(),
            new InMemoryRepo<VerificationOtp>(),
            new InMemoryRepo<AiUsageLog>(new[] { aiLog }),
            new FakeUnitOfWork(),
            NullLogger<DeleteAccountCommandHandler>.Instance,
            photoStorageService: photoStorage);

        var res = await handler.HandleAsync(new DeleteAccountCommand("user-dpdpa-1"));
        Assert.True(res.Succeeded);
        Assert.StartsWith("anon_", aiLog.UserId);
        Assert.Contains("user-dpdpa-1", photoStorage.DeletedUserDirectories);
    }

    [Fact]
    public async Task InMemoryAiQuotaReservationGate_SerializesSameUserRequests()
    {
        var gate = new InMemoryAiQuotaReservationGate();
        var active = 0;
        var maxConcurrent = 0;

        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var _lock = await gate.AcquireUserLockAsync("same-user");
            var nowActive = Interlocked.Increment(ref active);
            maxConcurrent = Math.Max(maxConcurrent, nowActive);
            await Task.Delay(15);
            Interlocked.Decrement(ref active);
        });

        await Task.WhenAll(tasks);
        Assert.Equal(1, maxConcurrent);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeOtpService : IOtpService
    {
        public string GenerateCode() => "123456";
        public VerificationOtp CreateOtp(string userId, string target, OtpChannel channel, string rawCode, int expiryMinutes = 10) => new() { UserId = userId, Target = target, Channel = channel };
        public bool ValidateCode(VerificationOtp otp, string rawCode, out string? failureReason) { failureReason = null; return true; }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(ApplicationUser user, string? displayName = null) => "fake";
        public ClaimsPrincipal? ValidateToken(string token) => new ClaimsPrincipal();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string password, string hash) => password == hash;
    }

    private sealed class FakePhotoStorageService : IPhotoStorageService
    {
        public List<string> DeletedUserDirectories { get; } = new();
        public Task<string> SaveMealPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult("/uploads/meal.jpg");
        public Task<string> SaveProgressPhotoAsync(string? userId, Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult("/uploads/prog.jpg");
        public Task<bool> DeletePhotoAsync(string relativePathOrUrl, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteUserDirectoryAsync(string userId, CancellationToken ct = default)
        {
            DeletedUserDirectories.Add(userId);
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryRepo<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items = new();
        public int UpdateCount { get; private set; }

        public InMemoryRepo(IEnumerable<T>? seed = null)
        {
            if (seed != null) _items.AddRange(seed);
        }

        private static string? GetId(T item) => typeof(T).GetProperty("Id")?.GetValue(item)?.ToString();

        public Task<T?> GetByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => GetId(x) == id));

        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Where(predicate).ToList());
        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Any(predicate));
        public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Count(predicate));
        public Task<List<T>> FindDeletedAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) => Task.FromResult(new List<T>());
        public Task<List<T>> FindAsync(QuerySpec<T> spec, CancellationToken ct = default) => Task.FromResult((spec.Filter != null ? _items.AsQueryable().Where(spec.Filter) : _items.AsQueryable()).ToList());
        public Task<List<TResult>> ProjectAsync<TResult>(QuerySpec<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default) => Task.FromResult((spec.Filter != null ? _items.AsQueryable().Where(spec.Filter) : _items.AsQueryable()).Select(selector).ToList());
        public Task<List<T>> FindDeletedAsync(QuerySpec<T> spec, CancellationToken ct = default) => Task.FromResult(new List<T>());
        public Task AddAsync(T entity, CancellationToken ct = default) { _items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(T entity, CancellationToken ct = default) { UpdateCount++; return Task.CompletedTask; }
        public Task DeleteAsync(string id, CancellationToken ct = default) { _items.RemoveAll(x => GetId(x) == id); return Task.CompletedTask; }
    }
}

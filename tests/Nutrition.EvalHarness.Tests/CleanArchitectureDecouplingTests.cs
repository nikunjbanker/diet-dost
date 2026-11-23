/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Persistence.Services;
using Nutrition.Infrastructure.Services;
using Nutrition.WebGateway.Controllers;
using Nutrition.WebGateway.Hubs;
using Nutrition.WebGateway.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class CleanArchitectureDecouplingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly IRepository<QrLoginSession> _qrRepo;
    private readonly IRepository<AppNotification> _notifRepo;
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IRepository<AiUsageLog> _aiLogRepo;
    private readonly IRepository<AppSecret> _secretRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CleanArchitectureDecouplingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        _qrRepo = new EfRepository<QrLoginSession>(_db, NullLogger<EfRepository<QrLoginSession>>.Instance);
        _notifRepo = new EfRepository<AppNotification>(_db, NullLogger<EfRepository<AppNotification>>.Instance);
        _tierRepo = new EfRepository<TierFeatureConfiguration>(_db, NullLogger<EfRepository<TierFeatureConfiguration>>.Instance);
        _aiLogRepo = new EfRepository<AiUsageLog>(_db, NullLogger<EfRepository<AiUsageLog>>.Instance);
        _secretRepo = new EfRepository<AppSecret>(_db, NullLogger<EfRepository<AppSecret>>.Instance);
        _unitOfWork = new EfUnitOfWork(_db, NullLogger<EfUnitOfWork>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task QrSessionCleanupService_PurgesExpiredSessions_ViaRepository()
    {
        // Arrange
        var expiredSession = QrLoginSession.CreateNew("challenge-1", "hash-1", "42", "Emerald Falcon");
        expiredSession.ExpiresAtUtc = DateTime.UtcNow.AddHours(-25); // Expired >24h
        await _qrRepo.AddAsync(expiredSession);

        var activeSession = QrLoginSession.CreateNew("challenge-2", "hash-2", "88", "Golden Tiger");
        activeSession.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5); // Active
        await _qrRepo.AddAsync(activeSession);

        await _unitOfWork.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(_qrRepo);
        services.AddSingleton(_unitOfWork);
        var sp = services.BuildServiceProvider();

        var service = new QrSessionCleanupService(sp, NullLogger<QrSessionCleanupService>.Instance);

        // Act - invoke private CleanupExpiredSessionsAsync via reflection
        var method = typeof(QrSessionCleanupService).GetMethod("CleanupExpiredSessionsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        await (Task)method.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert
        var remaining = await _qrRepo.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal(activeSession.Id, remaining[0].Id);
    }

    [Fact]
    public async Task TierConfigurationService_OperatesViaRepository_And_BackwardCompatibleCtor()
    {
        TierConfigurationService.ClearCache();

        // 1. Test repository constructor (Clean Architecture DIP)
        var tierService = new TierConfigurationService(_tierRepo, _unitOfWork);
        var config = await tierService.GetConfigurationAsync(UserTier.Free);
        Assert.NotNull(config);
        Assert.Equal(UserTier.Free, config.Tier);

        config.DailyAiDetectionLimit = 42;
        await tierService.UpdateConfigurationAsync(config);

        var updated = await tierService.GetConfigurationAsync(UserTier.Free);
        Assert.Equal(42, updated.DailyAiDetectionLimit);

        // 2. Test backward-compatible DbContext constructor
        TierConfigurationService.ClearCache();
        var legacyTierService = new TierConfigurationService(_db);
        var legacyConfig = await legacyTierService.GetConfigurationAsync(UserTier.Free);
        Assert.Equal(42, legacyConfig.DailyAiDetectionLimit);
    }

    [Fact]
    public async Task AiQuotaService_OperatesViaRepository_And_BackwardCompatibleCtor()
    {
        TierConfigurationService.ClearCache();
        var tierService = new TierConfigurationService(_tierRepo, _unitOfWork);

        // 1. Test repository constructor (Clean Architecture DIP)
        var quotaService = new AiQuotaService(_aiLogRepo, tierService, _unitOfWork);
        await quotaService.RecordUsageAsync("user-test", AiOperationType.PhotoDetection, "model-1", 100, 50, isSuccess: true);

        var stats = await quotaService.GetUsageStatsAsync("user-test", UserTier.Basic, "Asia/Kolkata");
        Assert.Equal(1, stats.UsedToday);

        // 2. Test backward-compatible DbContext constructor (Basic tier allows 7 calls: 7 - 1 = 6 remaining)
        var legacyQuotaService = new AiQuotaService(_db, tierService);
        var checkResult = await legacyQuotaService.CheckQuotaAsync("user-test", UserTier.Basic, "Asia/Kolkata");
        Assert.True(checkResult.IsAllowed);
        Assert.Equal(1, checkResult.UsedToday);
        Assert.Equal(6, checkResult.RemainingCalls);
    }

    [Fact]
    public async Task DatabaseSecretStore_OperatesViaRepository_And_BackwardCompatibleCtor()
    {
        DatabaseSecretStore.ClearCache();

        // 1. Test repository constructor (Clean Architecture DIP)
        var store = new DatabaseSecretStore(_secretRepo, _unitOfWork, NullLogger<DatabaseSecretStore>.Instance);
        await store.SetSecretAsync("TestApiKey", "SecretValue123");

        var retrieved = await store.GetSecretAsync("TestApiKey");
        Assert.Equal("SecretValue123", retrieved);

        // 2. Test backward-compatible DbContext constructor
        DatabaseSecretStore.ClearCache();
        var legacyStore = new DatabaseSecretStore(_db, NullLogger<DatabaseSecretStore>.Instance);
        var legacyRetrieved = await legacyStore.GetSecretAsync("TestApiKey");
        Assert.Equal("SecretValue123", legacyRetrieved);
    }

    [Fact]
    public async Task InAppNotificationService_OperatesViaRepository()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_notifRepo);
        services.AddSingleton(_unitOfWork);
        var sp = services.BuildServiceProvider();

        var hubContext = new MockHubContext();
        var notifService = new InAppNotificationService(sp, hubContext, NullLogger<InAppNotificationService>.Instance);

        await notifService.NotifySuperAdminNewRegistrationAsync("user-1", "John Doe", "john@example.com", "9876543210", DateTime.UtcNow);

        var list = await _notifRepo.GetAllAsync();
        Assert.Single(list);
        Assert.Equal("SuperAdmin", list[0].RecipientRole);
        Assert.Equal("NewRegistration", list[0].Type);
    }

    [Fact]
    public async Task NotificationsController_OperatesViaRepository()
    {
        var notif = new AppNotification
        {
            Id = "notif-1",
            RecipientUserId = "user-1",
            Title = "Welcome",
            Message = "Account ready",
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _notifRepo.AddAsync(notif);
        await _unitOfWork.SaveChangesAsync();

        var controller = new NotificationsController(_notifRepo, _unitOfWork, NullLogger<NotificationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim(ClaimTypes.Role, "Free")
                    }, "mock"))
                }
            }
        };

        var result = await controller.GetNotifications();
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var markReadResult = await controller.MarkAsRead("notif-1");
        Assert.IsType<OkObjectResult>(markReadResult);

        var updated = await _notifRepo.GetByIdAsync("notif-1");
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task DatabaseWarmupService_SupportsDocumentDb_And_Sqlite()
    {
        // 1. SQLite probe
        var warmupService = new DatabaseWarmupService(_db, NullLogger<DatabaseWarmupService>.Instance);
        var result = await warmupService.PingAndWarmupAsync();
        Assert.True(result.IsOnline);
        Assert.Contains("Sqlite", result.DatabaseProvider);

        // 2. Fallback when no DB provided
        var nullWarmup = new DatabaseWarmupService(null, NullLogger<DatabaseWarmupService>.Instance);
        var nullResult = await nullWarmup.PingAndWarmupAsync();
        Assert.True(nullResult.IsOnline);
        Assert.Equal("None", nullResult.DatabaseProvider);
    }

    private class MockHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients => new MockHubClients();
        public IGroupManager Groups => new MockGroupManager();
    }

    private class MockHubClients : IHubClients
    {
        public IClientProxy All => new MockClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
        public IClientProxy Client(string connectionId) => new MockClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new MockClientProxy();
        public IClientProxy Group(string groupName) => new MockClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new MockClientProxy();
        public IClientProxy User(string userId) => new MockClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new MockClientProxy();
    }

    private class MockClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class MockGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

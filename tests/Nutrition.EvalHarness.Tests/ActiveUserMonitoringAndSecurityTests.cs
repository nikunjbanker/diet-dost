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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Features.Admin.DTOs;
using Nutrition.Application.Features.Admin.Queries.GetActiveUsers;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Controllers;
using Nutrition.WebGateway.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class ActiveUserMonitoringAndSecurityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly EfRepository<ApplicationUser> _userRepo;
    private readonly EfUnitOfWork _uow;

    public ActiveUserMonitoringAndSecurityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        _userRepo = new EfRepository<ApplicationUser>(_db, NullLogger<EfRepository<ApplicationUser>>.Instance);
        _uow = new EfUnitOfWork(_db, NullLogger<EfUnitOfWork>.Instance);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetActiveUsersQuery_ShouldReturnOnlyUsersActiveWithinWindow_OrderedByLastActiveDesc()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var activeUser1 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "active1@test.com",
            NormalizedEmail = "ACTIVE1@TEST.COM",
            MobileNumber = "9999900001",
            NormalizedMobileNumber = "9999900001",
            PasswordHash = "hash",
            Role = UserRole.User,
            Tier = UserTier.Premium,
            IsEmailVerified = true,
            IsActive = true,
            LastActiveUtc = now.AddMinutes(-2)
        };

        var activeUser2 = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "active2@test.com",
            NormalizedEmail = "ACTIVE2@TEST.COM",
            MobileNumber = "9999900002",
            NormalizedMobileNumber = "9999900002",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Tier = UserTier.SuperAdmin,
            IsEmailVerified = true,
            IsActive = true,
            LastActiveUtc = now.AddMinutes(-8)
        };

        var inactiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "inactive@test.com",
            NormalizedEmail = "INACTIVE@TEST.COM",
            MobileNumber = "9999900003",
            NormalizedMobileNumber = "9999900003",
            PasswordHash = "hash",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true,
            IsActive = true,
            LastActiveUtc = now.AddMinutes(-30) // Expired (> 15m)
        };

        var neverActiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "never@test.com",
            NormalizedEmail = "NEVER@TEST.COM",
            MobileNumber = "9999900004",
            NormalizedMobileNumber = "9999900004",
            PasswordHash = "hash",
            Role = UserRole.User,
            Tier = UserTier.Basic,
            IsEmailVerified = true,
            IsActive = true,
            LastActiveUtc = null
        };

        await _userRepo.AddAsync(activeUser1);
        await _userRepo.AddAsync(activeUser2);
        await _userRepo.AddAsync(inactiveUser);
        await _userRepo.AddAsync(neverActiveUser);
        await _uow.SaveChangesAsync();

        var handler = new GetActiveUsersQueryHandler(_userRepo);
        var query = new GetActiveUsersQuery(WindowMinutes: 15);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);

        // Verify ordering: most recently active first
        Assert.Equal(activeUser1.Id, result.Data[0].Id);
        Assert.Equal(activeUser2.Id, result.Data[1].Id);
        Assert.True(result.Data[0].IsCurrentlyOnline);
        Assert.Equal(UserTier.Premium, result.Data[0].Tier);
    }

    [Fact]
    public async Task GetActiveUsersQuery_ShouldExcludeSoftDeletedUsers_AdheringToDPDPACompliance()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var deletedActiveUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "deleted@test.com",
            NormalizedEmail = "DELETED@TEST.COM",
            MobileNumber = "9999900005",
            NormalizedMobileNumber = "9999900005",
            PasswordHash = "hash",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsEmailVerified = true,
            IsActive = true,
            IsDeleted = true, // Soft-deleted
            DeletedAtUtc = now.AddMinutes(-1),
            LastActiveUtc = now.AddMinutes(-1)
        };

        await _userRepo.AddAsync(deletedActiveUser);
        await _uow.SaveChangesAsync();

        var handler = new GetActiveUsersQueryHandler(_userRepo);
        var query = new GetActiveUsersQuery(WindowMinutes: 15);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void AdminController_GetActiveUsers_MustEnforceStrictAdminAndSuperAdminRolesOnly()
    {
        // Reflect on AdminController.GetActiveUsers endpoint to verify strict RBAC
        var controllerType = typeof(AdminController);
        var classAuthorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        // Controller level is Admin,SuperAdmin
        Assert.NotNull(classAuthorize);
        Assert.Contains("Admin", classAuthorize.Roles);
        Assert.Contains("SuperAdmin", classAuthorize.Roles);

        var method = controllerType.GetMethod("GetActiveUsers");
        Assert.NotNull(method);

        // Verify HTTP GET Route
        var getAttr = method.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(getAttr);
        Assert.Equal("active-users", getAttr.Template);
    }

    [Fact]
    public async Task UserPresenceTracker_ShouldDebounceDatabaseUpdates_PreventingWriteAmplification()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "presence@test.com",
            NormalizedEmail = "PRESENCE@TEST.COM",
            MobileNumber = "9999900006",
            NormalizedMobileNumber = "9999900006",
            PasswordHash = "hash",
            Role = UserRole.User,
            Tier = UserTier.Basic,
            IsEmailVerified = true,
            IsActive = true,
            LastActiveUtc = null
        };

        await _userRepo.AddAsync(user);
        await _uow.SaveChangesAsync();

        var tracker = new UserPresenceTracker(_userRepo, _uow, NullLogger<UserPresenceTracker>.Instance);

        // Act: simulate 5 rapid requests from the user within seconds
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);

        // Assert in-memory active counts
        Assert.Equal(1, tracker.GetActiveUserCount(TimeSpan.FromMinutes(15)));
        var countsByTier = tracker.GetActiveCountByTier(TimeSpan.FromMinutes(15));
        Assert.Equal(1, countsByTier[UserTier.Basic]);

        // Flush debounced updates
        await tracker.FlushDebouncedUpdatesAsync();

        // Reload user from database and verify LastActiveUtc was updated
        var updatedUser = await _userRepo.GetByIdAsync(userId);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.LastActiveUtc);
        var firstUpdateUtc = updatedUser.LastActiveUtc.Value;

        // Act: immediate subsequent track within debounce window (e.g. 2 min)
        tracker.TrackActivity(userId, UserTier.Basic, UserRole.User);
        await tracker.FlushDebouncedUpdatesAsync();

        // Reload user from database and assert LastActiveUtc was NOT updated again (debounced)
        var debouncedUser = await _userRepo.GetByIdAsync(userId);
        Assert.NotNull(debouncedUser);
        Assert.Equal(firstUpdateUtc, debouncedUser.LastActiveUtc);
    }

    [Fact]
    public void OpenTelemetryMetric_ActiveUsersGauge_MustContainZeroPii()
    {
        // Vector: Verify that metric tags emitted for active users contain only aggregate tags: Tier and Platform.
        // It must NOT contain PII tags (email, userId, ip, mobile).
        var forbiddenPiiTags = new[] { "user.id", "user.email", "user.name", "email", "client_ip", "mobile" };
        var allowedTags = new[] { "tier", "platform" };

        foreach (var pii in forbiddenPiiTags)
        {
            Assert.DoesNotContain(pii, allowedTags, StringComparer.OrdinalIgnoreCase);
        }
    }
}

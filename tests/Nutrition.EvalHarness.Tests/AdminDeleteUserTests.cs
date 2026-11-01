using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.WebGateway.Controllers;
/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Features.Admin.Commands.UserManagement;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class AdminDeleteUserTests
{
    private class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private class InMemoryRepo<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items = new();

        public InMemoryRepo(IEnumerable<T>? seed = null)
        {
            if (seed != null) _items.AddRange(seed);
        }

        public Task<T?> GetByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x =>
                (x as ApplicationUser)?.Id == id ||
                (x as UserProfile)?.Id == id));

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
            if (predicate != null) query = query.Where(predicate);
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
            var entity = _items.FirstOrDefault(x =>
                (x as ApplicationUser)?.Id == id ||
                (x as UserProfile)?.Id == id);
            if (entity is Nutrition.Domain.Common.ISoftDeletable soft)
            {
                soft.IsDeleted = true;
                soft.DeletedAtUtc = DateTime.UtcNow;
            }
            else if (entity != null)
            {
                _items.Remove(entity);
            }
            return Task.CompletedTask;
        }
    }

    private static IConfiguration CreateConfiguration(string rootSuperAdminEmail = "nikunj_banker@hotmail.com")
    {
        var dict = new Dictionary<string, string?>
        {
            ["Auth:SuperAdminEmail"] = rootSuperAdminEmail
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task DeleteUser_WhenActorIsNotSuperAdmin_Returns403Forbidden()
    {
        // Arrange
        var userRepo = new InMemoryRepo<ApplicationUser>();
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "target-user-1",
            AdminUserId: "admin-user-1",
            CurrentAdminRole: nameof(UserRole.Admin) // Admin role, not SuperAdmin
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Only SuperAdmin can delete users.", result.Error);
    }

    [Fact]
    public async Task DeleteUser_WhenTargetDoesNotExist_Returns404NotFound()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = "superadmin-1",
            Email = "superadmin.other@dietdost.app",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { admin });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "non-existent-user",
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task DeleteUser_WhenTargetAlreadySoftDeleted_Returns404NotFound()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = "superadmin-1",
            Email = "superadmin.other@dietdost.app",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var target = new ApplicationUser
        {
            Id = "target-1",
            Email = "deleted.user@dietdost.app",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };
        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { admin, target });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "target-1",
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WhenSelfDeletionAttempted_Returns400BadRequest()
    {
        // Arrange
        var admin = new ApplicationUser
        {
            Id = "superadmin-1",
            Email = "superadmin.test@dietdost.app",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { admin });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "superadmin-1",
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("CannotDeleteSelf", result.ErrorCode);
        Assert.Contains("cannot delete your own account", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("nikunj_banker@hotmail.com")]
    [InlineData("NIKUNJ_BANKER@HOTMAIL.COM")]
    public async Task DeleteUser_WhenTargetIsRootSuperAdmin_Returns403Forbidden(string rootEmail)
    {
        // Arrange
        var otherSuperAdmin = new ApplicationUser
        {
            Id = "superadmin-other",
            Email = "other.superadmin@dietdost.app",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var rootUser = new ApplicationUser
        {
            Id = "root-user-id",
            Email = rootEmail,
            NormalizedEmail = rootEmail.ToUpperInvariant(),
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { otherSuperAdmin, rootUser });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration("nikunj_banker@hotmail.com");
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "root-user-id",
            AdminUserId: "superadmin-other",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("CannotDeleteRootSuperAdmin", result.ErrorCode);
        Assert.Contains("nikunj_banker@hotmail.com", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUser_WhenActorTierIsLowerThanTargetTier_Returns403Forbidden()
    {
        // Arrange: Actor has Tier Basic, Target has Tier Premium
        var actor = new ApplicationUser
        {
            Id = "actor-1",
            Email = "actor@dietdost.app",
            Role = UserRole.SuperAdmin, // bypass role check to test tier check
            Tier = UserTier.Basic
        };
        var target = new ApplicationUser
        {
            Id = "target-premium",
            Email = "premium.user@dietdost.app",
            Role = UserRole.User,
            Tier = UserTier.Premium
        };
        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { actor, target });
        var profileRepo = new InMemoryRepo<UserProfile>();
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "target-premium",
            AdminUserId: "actor-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("CannotDeleteHigherTierUser", result.ErrorCode);
        Assert.Contains("higher tier", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUser_WhenValidSuperAdmin_SuccessfullySoftDeletesUserAndCascadeSoftDeletesProfile()
    {
        // Arrange
        var superAdmin = new ApplicationUser
        {
            Id = "superadmin-1",
            Email = "superadmin.ops@dietdost.app",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin
        };
        var initialSecurityStamp = Guid.NewGuid().ToString("N");
        var targetUser = new ApplicationUser
        {
            Id = "target-to-delete",
            Email = "target.user@dietdost.app",
            Role = UserRole.User,
            Tier = UserTier.Basic,
            IsActive = true,
            IsDeleted = false,
            SecurityStamp = initialSecurityStamp
        };
        var targetProfile = new UserProfile
        {
            Id = "target-to-delete",
            Name = "Target User",
            IsDeleted = false
        };

        var userRepo = new InMemoryRepo<ApplicationUser>(new[] { superAdmin, targetUser });
        var profileRepo = new InMemoryRepo<UserProfile>(new[] { targetProfile });
        var uow = new FakeUnitOfWork();
        var config = CreateConfiguration();
        var handler = new AdminDeleteUserCommandHandler(userRepo, profileRepo, uow, config, NullLogger<AdminDeleteUserCommandHandler>.Instance);

        var command = new AdminDeleteUserCommand(
            TargetUserId: "target-to-delete",
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("target-to-delete", result.Data.UserId);
        Assert.Equal("target.user@dietdost.app", result.Data.Email);

        // User soft-delete assertions
        Assert.True(targetUser.IsDeleted);
        Assert.NotNull(targetUser.DeletedAtUtc);
        Assert.False(targetUser.IsActive);
        Assert.NotEqual(initialSecurityStamp, targetUser.SecurityStamp);

        // Profile cascade soft-delete assertions
        Assert.True(targetProfile.IsDeleted);
        Assert.NotNull(targetProfile.DeletedAtUtc);

        // UoW committed
        Assert.Equal(1, uow.SaveChangesCalls);

        // Login rejection assertion
        Assert.False(targetUser.CanLogin(out var rejectionReason));
        Assert.Contains("deactivated", rejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdminController_DeleteUserMethod_HasSuperAdminAuthorizeAndHttpDeleteAttributes()
    {
        var method = typeof(AdminController).GetMethod(nameof(AdminController.DeleteUser));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal("SuperAdmin", authAttr.Roles);

        var httpDeleteAttr = method.GetCustomAttribute<HttpDeleteAttribute>();
        Assert.NotNull(httpDeleteAttr);
        Assert.Equal("users/{id}", httpDeleteAttr.Template);
    }

    private class FakeDispatcher : IDispatcher
    {
        private readonly Func<object, object> _handler;
        public FakeDispatcher(Func<object, object> handler) => _handler = handler;
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default) =>
            Task.FromResult((TResponse)_handler(command));
        public Task<Result> SendAsync(ICommand command, CancellationToken ct = default) =>
            Task.FromResult((Result)_handler(command));
        public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct = default) =>
            Task.FromResult((TResponse)_handler(query));
    }

    [Fact]
    public async Task AdminController_DeleteUser_DispatchesCommandAndReturnsOk()
    {
        var fakeResult = Result<AdminDeleteUserResultDto>.Success(new AdminDeleteUserResultDto(
            "User deleted successfully.",
            "user-123",
            "deleted@dietdost.app",
            DateTime.UtcNow
        ));

        var dispatcher = new FakeDispatcher(cmd => fakeResult);
        var controller = new AdminController(dispatcher);
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "superadmin-id"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var response = await controller.DeleteUser("user-123");

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(fakeResult.Data, okResult.Value);
    }

    [Fact]
    public async Task AdminController_DeleteUser_WhenFails_ReturnsErrorStatusCode()
    {
        var fakeResult = Result<AdminDeleteUserResultDto>.Failure(
            "The root SuperAdmin account (nikunj_banker@hotmail.com) cannot be deleted by anyone.",
            "CannotDeleteRootSuperAdmin",
            403
        );

        var dispatcher = new FakeDispatcher(cmd => fakeResult);
        var controller = new AdminController(dispatcher);
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "superadmin-id"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var response = await controller.DeleteUser("root-id");

        var objResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(403, objResult.StatusCode);
    }
}

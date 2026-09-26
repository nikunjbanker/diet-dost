using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Features.Admin.Commands.UserManagement;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class AdminUserManagementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _db;
    private readonly EfRepository<ApplicationUser> _userRepo;
    private readonly EfRepository<UserProfile> _profileRepo;
    private readonly EfUnitOfWork _uow;
    private readonly Pbkdf2PasswordHasher _passwordHasher;

    public AdminUserManagementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DietTrackerDbContext(options);
        _db.Database.EnsureCreated();

        _userRepo = new EfRepository<ApplicationUser>(_db, NullLogger<EfRepository<ApplicationUser>>.Instance);
        _profileRepo = new EfRepository<UserProfile>(_db, NullLogger<EfRepository<UserProfile>>.Instance);
        _uow = new EfUnitOfWork(_db, NullLogger<EfUnitOfWork>.Instance);
        _passwordHasher = new Pbkdf2PasswordHasher();
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task CreateUser_AsSuperAdmin_Succeeds_AndCreatesProfile()
    {
        var handler = new AdminCreateUserCommandHandler(
            _userRepo,
            _profileRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminCreateUserCommandHandler>.Instance);

        var command = new AdminCreateUserCommand(
            Email: "new.patient@dietdost.app",
            Name: "New Patient",
            MobileNumber: "+919876543210",
            Password: "SecurePassword@2026!",
            Role: UserRole.User,
            Tier: UserTier.Premium,
            IsActive: true,
            IsEmailVerified: true,
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        var result = await handler.HandleAsync(command);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("new.patient@dietdost.app", result.Data.Email);
        Assert.Equal(UserTier.Premium, result.Data.Tier);
        Assert.Equal(UserRole.User, result.Data.Role);
        Assert.True(result.Data.IsActive);
        Assert.True(result.Data.IsEmailVerified);

        // Verify persisted in DB
        var userInDb = await _db.Users.FirstOrDefaultAsync(u => u.Email == "new.patient@dietdost.app");
        Assert.NotNull(userInDb);
        Assert.True(_passwordHasher.VerifyPassword("SecurePassword@2026!", userInDb.PasswordHash));

        var profileInDb = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userInDb.Id);
        Assert.NotNull(profileInDb);
        Assert.Equal("New Patient", profileInDb.Name);
    }

    [Fact]
    public async Task CreateUser_WeakPassword_FailsPolicy()
    {
        var handler = new AdminCreateUserCommandHandler(
            _userRepo,
            _profileRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminCreateUserCommandHandler>.Instance);

        var command = new AdminCreateUserCommand(
            Email: "weak.pwd@dietdost.app",
            Name: "Weak Pwd",
            MobileNumber: "9876543210",
            Password: "weak",
            Role: UserRole.User,
            Tier: UserTier.Free,
            IsActive: true,
            IsEmailVerified: true,
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        var result = await handler.HandleAsync(command);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("WeakPassword", result.ErrorCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsConflict()
    {
        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "duplicate@dietdost.app",
            NormalizedEmail = "DUPLICATE@DIETDOST.APP",
            MobileNumber = "9876543210",
            NormalizedMobileNumber = "9876543210",
            PasswordHash = _passwordHasher.HashPassword("Pass@1234567!"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsActive = true
        };
        _db.Users.Add(existingUser);
        await _db.SaveChangesAsync();

        var handler = new AdminCreateUserCommandHandler(
            _userRepo,
            _profileRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminCreateUserCommandHandler>.Instance);

        var command = new AdminCreateUserCommand(
            Email: "duplicate@dietdost.app",
            Name: "Duplicate",
            MobileNumber: "9876543210",
            Password: "SecurePassword@2026!",
            Role: UserRole.User,
            Tier: UserTier.Free,
            IsActive: true,
            IsEmailVerified: true,
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        var result = await handler.HandleAsync(command);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("EmailAlreadyExists", result.ErrorCode);
    }

    [Fact]
    public async Task CreateUser_NonSuperAdmin_CannotCreateSuperAdmin()
    {
        var handler = new AdminCreateUserCommandHandler(
            _userRepo,
            _profileRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminCreateUserCommandHandler>.Instance);

        var command = new AdminCreateUserCommand(
            Email: "god@dietdost.app",
            Name: "God Mode",
            MobileNumber: "9876543210",
            Password: "SecurePassword@2026!",
            Role: UserRole.SuperAdmin,
            Tier: UserTier.SuperAdmin,
            IsActive: true,
            IsEmailVerified: true,
            AdminUserId: "admin-1",
            CurrentAdminRole: nameof(UserRole.Admin)
        );

        var result = await handler.HandleAsync(command);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_UpdatesFieldsAndResetsPassword()
    {
        var user = new ApplicationUser
        {
            Id = "user-update-1",
            Email = "update.test@dietdost.app",
            NormalizedEmail = "UPDATE.TEST@DIETDOST.APP",
            MobileNumber = "9876543210",
            NormalizedMobileNumber = "9876543210",
            PasswordHash = _passwordHasher.HashPassword("OldPassword@123!"),
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var handler = new AdminUpdateUserCommandHandler(
            _userRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var command = new AdminUpdateUserCommand(
            TargetUserId: user.Id,
            MobileNumber: "+91 9988776655",
            Role: UserRole.Admin,
            Tier: UserTier.Premium,
            IsActive: true,
            IsEmailVerified: true,
            NewPassword: "BrandNewPassword@2026!",
            AdminUserId: "superadmin-1",
            CurrentAdminRole: nameof(UserRole.SuperAdmin)
        );

        var result = await handler.HandleAsync(command);

        Assert.True(result.Succeeded);
        Assert.Equal(UserRole.Admin, result.Data!.Role);
        Assert.Equal(UserTier.Premium, result.Data.Tier);

        var updatedInDb = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedInDb);
        Assert.Equal("+919988776655", updatedInDb.NormalizedMobileNumber);
        Assert.True(_passwordHasher.VerifyPassword("BrandNewPassword@2026!", updatedInDb.PasswordHash));
    }

    [Fact]
    public async Task UpdateUser_CannotDemoteOrLockSuperAdmin()
    {
        var superUser = new ApplicationUser
        {
            Id = "super-1",
            Email = "super@dietdost.app",
            NormalizedEmail = "SUPER@DIETDOST.APP",
            Role = UserRole.SuperAdmin,
            Tier = UserTier.SuperAdmin,
            IsActive = true
        };
        _db.Users.Add(superUser);
        await _db.SaveChangesAsync();

        var handler = new AdminUpdateUserCommandHandler(
            _userRepo,
            _uow,
            _passwordHasher,
            NullLogger<AdminUpdateUserCommandHandler>.Instance);

        // Try to demote role
        var demoteRoleCmd = new AdminUpdateUserCommand(
            superUser.Id, null, UserRole.User, null, null, null, null, "admin-1", nameof(UserRole.SuperAdmin));
        var res1 = await handler.HandleAsync(demoteRoleCmd);
        Assert.False(res1.Succeeded);
        Assert.Equal("CannotDemoteSuperAdmin", res1.ErrorCode);

        // Try to lock
        var lockCmd = new AdminUpdateUserCommand(
            superUser.Id, null, null, null, false, null, null, "admin-1", nameof(UserRole.SuperAdmin));
        var res2 = await handler.HandleAsync(lockCmd);
        Assert.False(res2.Succeeded);
        Assert.Equal("CannotLockSuperAdmin", res2.ErrorCode);
    }

    [Fact]
    public async Task LockAndUnlockUser_TogglesActiveAndInvalidatesSecurityStamp()
    {
        var user = new ApplicationUser
        {
            Id = "user-lock-test",
            Email = "lock.test@dietdost.app",
            NormalizedEmail = "LOCK.TEST@DIETDOST.APP",
            Role = UserRole.User,
            Tier = UserTier.Free,
            IsActive = true,
            IsEmailVerified = true,
            SecurityStamp = "initial-stamp-12345"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        Assert.True(user.CanLogin(out _));

        var statusHandler = new UpdateUserStatusCommandHandler(
            _userRepo,
            _uow,
            NullLogger<UpdateUserStatusCommandHandler>.Instance);

        // Lock user
        var lockResult = await statusHandler.HandleAsync(new UpdateUserStatusCommand(user.Id, false, "superadmin-1"));
        Assert.True(lockResult.Succeeded);
        Assert.False(lockResult.Data!.IsActive);

        var lockedInDb = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(lockedInDb);
        Assert.False(lockedInDb.IsActive);
        Assert.NotEqual("initial-stamp-12345", lockedInDb.SecurityStamp);
        Assert.False(lockedInDb.CanLogin(out var rejection));
        Assert.Contains("deactivated", rejection);

        // Unlock user
        var unlockResult = await statusHandler.HandleAsync(new UpdateUserStatusCommand(user.Id, true, "superadmin-1"));
        Assert.True(unlockResult.Succeeded);
        Assert.True(unlockResult.Data!.IsActive);

        var unlockedInDb = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(unlockedInDb);
        Assert.True(unlockedInDb.IsActive);
        Assert.True(unlockedInDb.CanLogin(out _));
    }
}

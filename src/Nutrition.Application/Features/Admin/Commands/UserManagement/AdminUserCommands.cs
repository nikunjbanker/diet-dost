using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Admin.DTOs;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Admin.Commands.UserManagement;

public record UpdateUserTierCommand(
    string TargetUserId,
    UserTier NewTier,
    string AdminUserId
) : ICommand<Result<UpdateUserTierResultDto>>;

public record UpdateUserTierResultDto(
    string Message,
    string UserId,
    UserTier Tier
);

public class UpdateUserTierCommandHandler : ICommandHandler<UpdateUserTierCommand, Result<UpdateUserTierResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateUserTierCommandHandler> _logger;

    public UpdateUserTierCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<UpdateUserTierCommandHandler> logger)
    {
        _userRepo = userRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<UpdateUserTierResultDto>> HandleAsync(UpdateUserTierCommand request, CancellationToken ct = default)
    {
        var targetUser = await _userRepo.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser == null)
            return Result<UpdateUserTierResultDto>.NotFound("User not found.");

        if (targetUser.Role == UserRole.SuperAdmin && request.NewTier != UserTier.SuperAdmin)
        {
            return Result<UpdateUserTierResultDto>.Failure("Cannot demote the SuperAdmin tier.", "CannotDemoteSuperAdmin", 400);
        }

        targetUser.Tier = request.NewTier;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed tier of User {TargetId} to {NewTier}",
            request.AdminUserId, request.TargetUserId, request.NewTier);

        return Result<UpdateUserTierResultDto>.Success(new UpdateUserTierResultDto(
            $"User tier updated to {request.NewTier}.",
            request.TargetUserId,
            targetUser.Tier
        ));
    }
}

public record UpdateUserRoleCommand(
    string TargetUserId,
    UserRole NewRole,
    string AdminUserId,
    string CurrentUserRole
) : ICommand<Result<UpdateUserRoleResultDto>>;

public record UpdateUserRoleResultDto(
    string Message,
    string UserId,
    UserRole Role
);

public class UpdateUserRoleCommandHandler : ICommandHandler<UpdateUserRoleCommand, Result<UpdateUserRoleResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateUserRoleCommandHandler> _logger;

    public UpdateUserRoleCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<UpdateUserRoleCommandHandler> logger)
    {
        _userRepo = userRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<UpdateUserRoleResultDto>> HandleAsync(UpdateUserRoleCommand request, CancellationToken ct = default)
    {
        if (request.CurrentUserRole != nameof(UserRole.SuperAdmin) && request.NewRole == UserRole.SuperAdmin)
        {
            return Result<UpdateUserRoleResultDto>.Forbidden("Only SuperAdmin can assign SuperAdmin role.");
        }

        var targetUser = await _userRepo.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser == null)
            return Result<UpdateUserRoleResultDto>.NotFound("User not found.");

        if (targetUser.Role == UserRole.SuperAdmin && request.NewRole != UserRole.SuperAdmin)
        {
            return Result<UpdateUserRoleResultDto>.Failure("Cannot demote the SuperAdmin role.", "CannotDemoteSuperAdmin", 400);
        }

        targetUser.Role = request.NewRole;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed role of User {TargetId} to {NewRole}",
            request.AdminUserId, request.TargetUserId, request.NewRole);

        return Result<UpdateUserRoleResultDto>.Success(new UpdateUserRoleResultDto(
            $"User role updated to {request.NewRole}.",
            request.TargetUserId,
            targetUser.Role
        ));
    }
}

public record UpdateUserStatusCommand(
    string TargetUserId,
    bool IsActive,
    string AdminUserId
) : ICommand<Result<UpdateUserStatusResultDto>>;

public record UpdateUserStatusResultDto(
    string Message,
    string UserId,
    bool IsActive
);

public class UpdateUserStatusCommandHandler : ICommandHandler<UpdateUserStatusCommand, Result<UpdateUserStatusResultDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateUserStatusCommandHandler> _logger;

    public UpdateUserStatusCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        ILogger<UpdateUserStatusCommandHandler> logger)
    {
        _userRepo = userRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<UpdateUserStatusResultDto>> HandleAsync(UpdateUserStatusCommand request, CancellationToken ct = default)
    {
        var targetUser = await _userRepo.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser == null)
            return Result<UpdateUserStatusResultDto>.NotFound("User not found.");

        if (targetUser.Role == UserRole.SuperAdmin && !request.IsActive)
        {
            return Result<UpdateUserStatusResultDto>.Failure("Cannot deactivate or lock the SuperAdmin account.", "CannotLockSuperAdmin", 400);
        }

        // When locked, immediately invalidate active tokens & sessions
        if (!request.IsActive)
        {
            targetUser.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        targetUser.IsActive = request.IsActive;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed active status of User {TargetId} to {IsActive}",
            request.AdminUserId, request.TargetUserId, request.IsActive);

        return Result<UpdateUserStatusResultDto>.Success(new UpdateUserStatusResultDto(
            $"User status updated to {(request.IsActive ? "Active" : "Locked")}.",
            request.TargetUserId,
            targetUser.IsActive
        ));
    }
}

public record AdminCreateUserCommand(
    string Email,
    string? Name,
    string MobileNumber,
    string Password,
    UserRole Role,
    UserTier Tier,
    bool IsActive,
    bool IsEmailVerified,
    string AdminUserId,
    string CurrentAdminRole
) : ICommand<Result<AdminUserSummaryDto>>;

public class AdminCreateUserCommandHandler : ICommandHandler<AdminCreateUserCommand, Result<AdminUserSummaryDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminCreateUserCommandHandler> _logger;

    public AdminCreateUserCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        ILogger<AdminCreateUserCommandHandler> logger)
    {
        _userRepo = userRepo;
        _profileRepo = profileRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result<AdminUserSummaryDto>> HandleAsync(AdminCreateUserCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<AdminUserSummaryDto>.Failure("Email is mandatory.", "InvalidEmail", 400);

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
            return Result<AdminUserSummaryDto>.Failure("Mobile phone number is mandatory.", "InvalidMobile", 400);

        if (string.IsNullOrWhiteSpace(request.Password))
            return Result<AdminUserSummaryDto>.Failure("Initial password is required.", "InvalidPassword", 400);

        if (!PasswordPolicy.IsValid(request.Password, out var pwdError))
            return Result<AdminUserSummaryDto>.Failure(pwdError ?? "Password violates strength requirements.", "WeakPassword", 400);

        if (request.CurrentAdminRole != nameof(UserRole.SuperAdmin) && request.Role == UserRole.SuperAdmin)
            return Result<AdminUserSummaryDto>.Forbidden("Only SuperAdmin can provision SuperAdmin accounts.");

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(request.Email);
        var existing = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (existing != null)
            return Result<AdminUserSummaryDto>.Failure("A user with this email address already exists.", "EmailAlreadyExists", 409);

        var normalizedMobile = ApplicationUser.NormalizePhoneNumber(request.MobileNumber);

        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            MobileNumber = request.MobileNumber.Trim(),
            NormalizedMobileNumber = normalizedMobile,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Role = request.Role,
            Tier = request.Tier,
            IsActive = request.IsActive,
            IsEmailVerified = request.IsEmailVerified,
            IsMobileVerified = true,
            CreatedAtUtc = DateTime.UtcNow,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsVersionAccepted = "AdminProvisioned-v1.0",
            HealthConsentAcceptedAtUtc = DateTime.UtcNow,
            HealthConsentVersionAccepted = "AdminProvisioned-v1.0",
            ConsentIpAddress = "127.0.0.1",
            ConsentUserAgent = $"AdminProvisionedBy:{request.AdminUserId}"
        };

        await _userRepo.AddAsync(newUser, ct);

        var profile = new UserProfile
        {
            Id = newUser.Id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? request.Email.Split('@')[0] : request.Name.Trim(),
            Sex = BiologicalSex.Male,
            Age = 30,
            HeightCm = 170,
            CurrentWeightKg = 70,
            TargetWeightKg = 65,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Sedentary,
            DietaryPreference = DietaryPreference.LactoVeg,
            RegionalCuisine = "North Indian",
            Timezone = "Asia/Kolkata"
        };
        await _profileRepo.AddAsync(profile, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} successfully created user {Email} with role {Role} and tier {Tier}",
            request.AdminUserId, newUser.Email, newUser.Role, newUser.Tier);

        return Result<AdminUserSummaryDto>.Success(new AdminUserSummaryDto(
            Id: newUser.Id,
            Email: newUser.Email,
            MobileNumber: newUser.MobileNumber,
            Role: newUser.Role,
            Tier: newUser.Tier,
            IsEmailVerified: newUser.IsEmailVerified,
            IsMobileVerified: newUser.IsMobileVerified,
            IsActive: newUser.IsActive,
            TermsAcceptedAtUtc: newUser.TermsAcceptedAtUtc,
            TermsVersionAccepted: newUser.TermsVersionAccepted,
            HealthConsentAcceptedAtUtc: newUser.HealthConsentAcceptedAtUtc,
            HealthConsentVersionAccepted: newUser.HealthConsentVersionAccepted,
            ConsentIpAddress: newUser.ConsentIpAddress,
            CreatedAtUtc: newUser.CreatedAtUtc,
            LastLoginAtUtc: null,
            TodayAiDetectionsCount: 0
        ));
    }
}

public record AdminUpdateUserCommand(
    string TargetUserId,
    string? MobileNumber,
    UserRole? Role,
    UserTier? Tier,
    bool? IsActive,
    bool? IsEmailVerified,
    string? NewPassword,
    string AdminUserId,
    string CurrentAdminRole
) : ICommand<Result<AdminUserSummaryDto>>;

public class AdminUpdateUserCommandHandler : ICommandHandler<AdminUpdateUserCommand, Result<AdminUserSummaryDto>>
{
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminUpdateUserCommandHandler> _logger;

    public AdminUpdateUserCommandHandler(
        IRepository<ApplicationUser> userRepo,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        ILogger<AdminUpdateUserCommandHandler> logger)
    {
        _userRepo = userRepo;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result<AdminUserSummaryDto>> HandleAsync(AdminUpdateUserCommand request, CancellationToken ct = default)
    {
        var targetUser = await _userRepo.GetByIdAsync(request.TargetUserId, ct);
        if (targetUser == null)
            return Result<AdminUserSummaryDto>.NotFound("User not found.");

        // Guard SuperAdmin demotion/deactivation
        if (targetUser.Role == UserRole.SuperAdmin)
        {
            if (request.Role.HasValue && request.Role.Value != UserRole.SuperAdmin)
                return Result<AdminUserSummaryDto>.Failure("Cannot demote the SuperAdmin role.", "CannotDemoteSuperAdmin", 400);

            if (request.Tier.HasValue && request.Tier.Value != UserTier.SuperAdmin)
                return Result<AdminUserSummaryDto>.Failure("Cannot demote the SuperAdmin tier.", "CannotDemoteSuperAdmin", 400);

            if (request.IsActive.HasValue && !request.IsActive.Value)
                return Result<AdminUserSummaryDto>.Failure("Cannot lock or deactivate the SuperAdmin account.", "CannotLockSuperAdmin", 400);
        }

        // Only SuperAdmin can assign SuperAdmin role
        if (request.Role.HasValue && request.Role.Value == UserRole.SuperAdmin && request.CurrentAdminRole != nameof(UserRole.SuperAdmin))
        {
            return Result<AdminUserSummaryDto>.Forbidden("Only SuperAdmin can assign SuperAdmin role.");
        }

        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            targetUser.MobileNumber = request.MobileNumber.Trim();
            targetUser.NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber(request.MobileNumber);
        }

        if (request.Role.HasValue)
        {
            targetUser.Role = request.Role.Value;
        }

        if (request.Tier.HasValue)
        {
            targetUser.Tier = request.Tier.Value;
        }

        if (request.IsEmailVerified.HasValue)
        {
            targetUser.IsEmailVerified = request.IsEmailVerified.Value;
        }

        if (request.IsActive.HasValue)
        {
            if (targetUser.IsActive && !request.IsActive.Value)
            {
                // Invalidate all tokens immediately on lock
                targetUser.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            targetUser.IsActive = request.IsActive.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (!PasswordPolicy.IsValid(request.NewPassword, out var pwdError))
                return Result<AdminUserSummaryDto>.Failure(pwdError ?? "Password violates strength requirements.", "WeakPassword", 400);

            targetUser.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            targetUser.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} updated user {TargetUserId}", request.AdminUserId, request.TargetUserId);

        return Result<AdminUserSummaryDto>.Success(new AdminUserSummaryDto(
            Id: targetUser.Id,
            Email: targetUser.Email,
            MobileNumber: targetUser.MobileNumber,
            Role: targetUser.Role,
            Tier: targetUser.Tier,
            IsEmailVerified: targetUser.IsEmailVerified,
            IsMobileVerified: targetUser.IsMobileVerified,
            IsActive: targetUser.IsActive,
            TermsAcceptedAtUtc: targetUser.TermsAcceptedAtUtc,
            TermsVersionAccepted: targetUser.TermsVersionAccepted,
            HealthConsentAcceptedAtUtc: targetUser.HealthConsentAcceptedAtUtc,
            HealthConsentVersionAccepted: targetUser.HealthConsentVersionAccepted,
            ConsentIpAddress: targetUser.ConsentIpAddress,
            CreatedAtUtc: targetUser.CreatedAtUtc,
            LastLoginAtUtc: targetUser.LastLoginAtUtc,
            TodayAiDetectionsCount: 0
        ));
    }
}

public record AdminLockUserCommand(
    string TargetUserId,
    bool IsLocked,
    string AdminUserId
) : ICommand<Result<UpdateUserStatusResultDto>>;

public class AdminLockUserCommandHandler : ICommandHandler<AdminLockUserCommand, Result<UpdateUserStatusResultDto>>
{
    private readonly IDispatcher _dispatcher;

    public AdminLockUserCommandHandler(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<Result<UpdateUserStatusResultDto>> HandleAsync(AdminLockUserCommand request, CancellationToken ct = default)
    {
        return await _dispatcher.SendAsync(new UpdateUserStatusCommand(request.TargetUserId, !request.IsLocked, request.AdminUserId), ct);
    }
}

using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Identity;

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

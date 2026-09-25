using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

public record UpdateUserTierRequest(UserTier Tier);
public record UpdateUserRoleRequest(UserRole Role);
public record UpdateUserStatusRequest(bool IsActive);
public record UpdateTierConfigRequest(
    int DailyAiDetectionLimit,
    bool AllowPhotoCompare,
    bool AllowDataExport,
    int AnalyticsHistoryDays,
    string Description
);

public record AdminUserSummaryDto(
    string Id,
    string Email,
    string MobileNumber,
    UserRole Role,
    UserTier Tier,
    bool IsEmailVerified,
    bool IsMobileVerified,
    bool IsActive,
    DateTime? TermsAcceptedAtUtc,
    string? TermsVersionAccepted,
    DateTime? HealthConsentAcceptedAtUtc,
    string? HealthConsentVersionAccepted,
    string? ConsentIpAddress,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    int TodayAiDetectionsCount
);

[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly DietTrackerDbContext _db;
    private readonly ITierConfigurationService _tierConfigService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        DietTrackerDbContext db,
        ITierConfigurationService tierConfigService,
        ILogger<AdminController> logger)
    {
        _db = db;
        _tierConfigService = tierConfigService;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] UserTier? tier = null,
        [FromQuery] UserRole? role = null,
        CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(searchLower) ||
                                     u.MobileNumber.Contains(searchLower));
        }

        if (tier.HasValue)
        {
            query = query.Where(u => u.Tier == tier.Value);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync(ct);

        var todayUtc = DateTime.UtcNow.Date;
        var todayAiCounts = await _db.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.TimestampUtc >= todayUtc && l.IsSuccess)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var dtos = users.Select(u => new AdminUserSummaryDto(
            Id: u.Id,
            Email: u.Email,
            MobileNumber: u.MobileNumber,
            Role: u.Role,
            Tier: u.Tier,
            IsEmailVerified: u.IsEmailVerified,
            IsMobileVerified: u.IsMobileVerified,
            IsActive: u.IsActive,
            TermsAcceptedAtUtc: u.TermsAcceptedAtUtc,
            TermsVersionAccepted: u.TermsVersionAccepted,
            HealthConsentAcceptedAtUtc: u.HealthConsentAcceptedAtUtc,
            HealthConsentVersionAccepted: u.HealthConsentVersionAccepted,
            ConsentIpAddress: u.ConsentIpAddress,
            CreatedAtUtc: u.CreatedAtUtc,
            LastLoginAtUtc: u.LastLoginAtUtc,
            TodayAiDetectionsCount: todayAiCounts.TryGetValue(u.Id, out var count) ? count : 0
        )).ToList();

        return Ok(dtos);
    }

    [HttpPut("users/{id}/tier")]
    public async Task<IActionResult> UpdateUserTier(
        [FromRoute] string id,
        [FromBody] UpdateUserTierRequest request,
        CancellationToken ct = default)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (targetUser == null)
            return NotFound(new { error = "User not found." });

        // SuperAdmin protection
        if (targetUser.Role == UserRole.SuperAdmin && request.Tier != UserTier.SuperAdmin)
        {
            return BadRequest(new { error = "Cannot demote the SuperAdmin tier." });
        }

        targetUser.Tier = request.Tier;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed tier of User {TargetId} to {NewTier}",
            User.GetUserId(), id, request.Tier);

        return Ok(new
        {
            message = $"User tier updated to {request.Tier}.",
            userId = id,
            tier = targetUser.Tier
        });
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(
        [FromRoute] string id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken ct = default)
    {
        var currentRole = User.GetRole();
        if (currentRole != nameof(UserRole.SuperAdmin) && request.Role == UserRole.SuperAdmin)
        {
            return Forbid();
        }

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (targetUser == null)
            return NotFound(new { error = "User not found." });

        // SuperAdmin account cannot be demoted
        if (targetUser.Role == UserRole.SuperAdmin && request.Role != UserRole.SuperAdmin)
        {
            return BadRequest(new { error = "Cannot demote the SuperAdmin role." });
        }

        targetUser.Role = request.Role;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed role of User {TargetId} to {NewRole}",
            User.GetUserId(), id, request.Role);

        return Ok(new
        {
            message = $"User role updated to {request.Role}.",
            userId = id,
            role = targetUser.Role
        });
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] string id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken ct = default)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (targetUser == null)
            return NotFound(new { error = "User not found." });

        // SuperAdmin protection: cannot lock/deactivate SuperAdmin
        if (targetUser.Role == UserRole.SuperAdmin && !request.IsActive)
        {
            return BadRequest(new { error = "Cannot deactivate or lock the SuperAdmin account." });
        }

        targetUser.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} changed active status of User {TargetId} to {IsActive}",
            User.GetUserId(), id, request.IsActive);

        return Ok(new
        {
            message = $"User status updated to {(request.IsActive ? "Active" : "Locked")}.",
            userId = id,
            isActive = targetUser.IsActive
        });
    }

    [HttpGet("tier-configs")]
    public async Task<IActionResult> GetTierConfigs(CancellationToken ct = default)
    {
        var configs = await _tierConfigService.GetAllConfigurationsAsync(ct);
        return Ok(configs);
    }

    [HttpPut("tier-configs/{tier}")]
    public async Task<IActionResult> UpdateTierConfig(
        [FromRoute] UserTier tier,
        [FromBody] UpdateTierConfigRequest request,
        CancellationToken ct = default)
    {
        var config = await _tierConfigService.GetConfigurationAsync(tier, ct);
        config.DailyAiDetectionLimit = request.DailyAiDetectionLimit;
        config.AllowPhotoCompare = request.AllowPhotoCompare;
        config.AllowDataExport = request.AllowDataExport;
        config.AnalyticsHistoryDays = request.AnalyticsHistoryDays;
        config.Description = request.Description;
        config.UpdatedByUserId = User.GetUserId();

        var updated = await _tierConfigService.UpdateConfigurationAsync(config, ct);

        _logger.LogInformation("Admin {AdminId} updated tier config for {Tier}",
            User.GetUserId(), tier);

        return Ok(new
        {
            message = $"Tier configuration for {tier} updated successfully.",
            configuration = updated
        });
    }

    [HttpGet("ai-logs")]
    public async Task<IActionResult> GetAiLogs(
        [FromQuery] string? userId = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var query = _db.AiUsageLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(l => l.UserId == userId);
        }

        var logs = await query
            .OrderByDescending(l => l.TimestampUtc)
            .Take(Math.Min(limit, 100))
            .ToListAsync(ct);

        return Ok(logs);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Admin.Commands.TierConfiguration;
using Nutrition.Application.Features.Admin.Commands.UserManagement;
using Nutrition.Application.Features.Admin.Queries.GetAdminUsers;
using Nutrition.Domain.Model.Identity;
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

/// <summary>
/// Thin Presentation Controller for Administrator & SuperAdmin Operations.
/// Dispatches all governance, tier adjustments, and user state management to Application CQRS handlers.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] UserTier? tier = null,
        [FromQuery] UserRole? role = null,
        CancellationToken ct = default)
    {
        var query = new GetAdminUsersQuery(search, tier, role);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("users/{id}/tier")]
    public async Task<IActionResult> UpdateUserTier(
        [FromRoute] string id,
        [FromBody] UpdateUserTierRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = User.GetUserId() ?? "System";
        var command = new UpdateUserTierCommand(id, request.Tier, adminUserId);
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            message = result.Data!.Message,
            userId = result.Data.UserId,
            tier = result.Data.Tier
        });
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(
        [FromRoute] string id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = User.GetUserId() ?? "System";
        var currentRole = User.GetRole() ?? string.Empty;
        var command = new UpdateUserRoleCommand(id, request.Role, adminUserId, currentRole);
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            message = result.Data!.Message,
            userId = result.Data.UserId,
            role = result.Data.Role
        });
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] string id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = User.GetUserId() ?? "System";
        var command = new UpdateUserStatusCommand(id, request.IsActive, adminUserId);
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            message = result.Data!.Message,
            userId = result.Data.UserId,
            isActive = result.Data.IsActive
        });
    }

    [HttpGet("tier-configs")]
    public async Task<IActionResult> GetTierConfigs(CancellationToken ct = default)
    {
        var result = await _dispatcher.QueryAsync(new GetTierConfigsQuery(), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("tier-configs/{tier}")]
    public async Task<IActionResult> UpdateTierConfig(
        [FromRoute] UserTier tier,
        [FromBody] UpdateTierConfigRequest request,
        CancellationToken ct = default)
    {
        var adminUserId = User.GetUserId();
        var command = new UpdateTierConfigCommand(
            tier,
            request.DailyAiDetectionLimit,
            request.AllowPhotoCompare,
            request.AllowDataExport,
            request.AnalyticsHistoryDays,
            request.Description,
            adminUserId
        );

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            message = result.Data!.Message,
            configuration = result.Data.Configuration
        });
    }

    [HttpGet("ai-logs")]
    public async Task<IActionResult> GetAiLogs(
        [FromQuery] string? userId = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await _dispatcher.QueryAsync(new GetAiLogsQuery(userId, limit), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Services;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly ClinicalDietitianService _dietitianService;

    public AnalyticsController(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyLedger(
        [FromQuery] string? userId,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (targetUserId != currentUserId && !User.IsAdminOrSuper())
        {
            return Forbid();
        }

        var dateOnly = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(targetUserId, dateOnly, ct);
        return Ok(ledger);
    }

    [HttpGet("projections")]
    public async Task<IActionResult> GetProjections(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (targetUserId != currentUserId && !User.IsAdminOrSuper())
        {
            return Forbid();
        }

        var projections = await _dietitianService.GetAnalyticsProjectionAsync(targetUserId, period, ct);
        return Ok(projections);
    }
}

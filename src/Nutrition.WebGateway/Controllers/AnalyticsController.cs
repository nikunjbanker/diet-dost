using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly ITierConfigurationService _tierConfigService;

    public AnalyticsController(
        ClinicalDietitianService dietitianService,
        ITierConfigurationService tierConfigService)
    {
        _dietitianService = dietitianService;
        _tierConfigService = tierConfigService;
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

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;
        var config = await _tierConfigService.GetConfigurationAsync(userTier, ct);

        var normPeriod = (period ?? "7D").ToUpperInvariant();
        int requestedDays = normPeriod switch
        {
            "1D" or "DAILY" => 1,
            "7D" or "WEEKLY" => 7,
            "30D" or "MONTHLY" => 30,
            "365D" or "YEARLY" or "1Y" => 365,
            _ => 7
        };

        if (requestedDays > config.AnalyticsHistoryDays && !User.IsAdminOrSuper())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureTierUpgradeRequired",
                message = $"Historical analytics for {normPeriod} requires an upgraded tier (Your plan allows up to {config.AnalyticsHistoryDays} days).",
                allowedDays = config.AnalyticsHistoryDays,
                requestedPeriod = normPeriod
            });
        }

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (targetUserId != currentUserId && !User.IsAdminOrSuper())
        {
            return Forbid();
        }

        var projections = await _dietitianService.GetAnalyticsProjectionAsync(targetUserId, period, ct);
        return Ok(projections);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportData(
        [FromQuery] string? userId,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;
        var config = await _tierConfigService.GetConfigurationAsync(userTier, ct);

        if (!config.AllowDataExport && !User.IsAdminOrSuper())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureTierUpgradeRequired",
                message = "Data export is a Premium tier feature. Please upgrade your plan."
            });
        }

        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (targetUserId != currentUserId && !User.IsAdminOrSuper())
        {
            return Forbid();
        }

        var meals = await _dietitianService.GetMealHistoryAsync(targetUserId, "365D", null, null, ct);
        var csv = new StringBuilder();
        csv.AppendLine("LoggedAtUtc,MealType,TotalCalories,TotalProteinGrams,TotalCarbsGrams,TotalFatGrams,TotalSodiumMg,ItemCount");
        foreach (var m in meals)
        {
            csv.AppendLine($"{m.LoggedAt:O},{m.MealType},{m.TotalCalories:F1},{m.TotalProteinGrams:F1},{m.TotalCarbsGrams:F1},{m.TotalFatGrams:F1},{m.TotalSodiumMg:F1},{m.Items?.Count ?? 0}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"diet_dost_export_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

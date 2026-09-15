using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Services;

namespace Nutrition.WebGateway.Controllers;

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
        [FromQuery] string userId,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var dateOnly = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(userId, dateOnly, ct);
        return Ok(ledger);
    }

    [HttpGet("projections")]
    public async Task<IActionResult> GetProjections(
        [FromQuery] string userId,
        [FromQuery] string period = "7D",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var projections = await _dietitianService.GetAnalyticsProjectionAsync(userId, period, ct);
        return Ok(projections);
    }
}

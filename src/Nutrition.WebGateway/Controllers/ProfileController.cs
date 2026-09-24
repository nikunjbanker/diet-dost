using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Profile;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly ClinicalDietitianService _dietitianService;

    public ProfileController(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    [HttpGet("{userId?}")]
    public async Task<IActionResult> GetProfile(string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // Enforce tenant boundary: non-admins cannot inspect other users' health profiles (OWASP A01)
        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (targetUserId != currentUserId && !User.IsAdminOrSuper())
        {
            return Forbid();
        }

        var profile = await _dietitianService.GetProfileAsync(targetUserId, ct);
        if (profile == null) return NotFound(new { message = "User profile not found. Please complete clinical onboarding." });

        var budget = _dietitianService.CalculateTargetBudget(profile);
        var macros = _dietitianService.CalculateMacros(profile);

        return Ok(new
        {
            profile,
            budget,
            macros
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveProfile([FromBody] UserProfile profile, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // Bind profile ID strictly to authenticated identity
        profile.Id = currentUserId;

        try
        {
            var saved = await _dietitianService.SaveProfileAsync(profile, ct);
            var budget = _dietitianService.CalculateTargetBudget(saved);
            var macros = _dietitianService.CalculateMacros(saved);

            return Ok(new
            {
                profile = saved,
                budget,
                macros,
                message = "Clinical profile saved successfully. Caloric budget and macronutrients calculated per ICMR-NIN & WHO standards."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, zeroAssumptionViolation = true });
        }
    }
}

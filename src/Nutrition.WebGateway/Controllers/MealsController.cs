using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Agents;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Meals.Commands.AnalyzeTextMeal;
using Nutrition.Application.Features.Meals.Commands.ConfirmMeal;
using Nutrition.Application.Features.Meals.Commands.Corrections;
using Nutrition.Application.Features.Meals.Commands.DeleteMeal;
using Nutrition.Application.Features.Meals.Commands.SubmitFeedback;
using Nutrition.Application.Features.Meals.Commands.UpdateMeal;
using Nutrition.Application.Features.Meals.Commands.UploadAndAnalyzeMeal;
using Nutrition.Application.Features.Meals.Queries.EstimateFoodItem;
using Nutrition.Application.Features.Meals.Queries.ExportMeals;
using Nutrition.Application.Features.Meals.Queries.GetAiFeedbacks;
using Nutrition.Application.Features.Meals.Queries.GetAiQuota;
using Nutrition.Application.Features.Meals.Queries.GetCorrections;
using Nutrition.Application.Features.Meals.Queries.GetMealById;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Extensions;

namespace Nutrition.WebGateway.Controllers;

public record TextAnalysisRequest(string? UserId, string Description, string? MealType);

public record FoodItemEstimateRequest(string Name, string? Portion, bool UseAi = true, string? UserId = null, string? MealType = null);

public record AiFeedbackRequest(
    string UserId,
    string? MealLogId,
    string DishName,
    string DetectedByModel,
    double ConfidenceScore,
    string Rating,
    string? Remarks,
    List<IndianMealItemDto>? Items = null
);

/// <summary>
/// Thin Presentation Controller for Meal Analysis, Confirmation, History, and AI Feedback.
/// Dispatches all clinical and AI operations to Application CQRS handlers.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MealsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public MealsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ImageUploadValidator.MaxSizeBytes)]
    public async Task<IActionResult> UploadAndAnalyzeMeal(
        [FromForm] IFormFile? image,
        [FromForm] string? userId,
        [FromForm] string? regionalContext,
        [FromForm] string? mealType,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Please provide a valid meal photo." });

        using var stream = image.OpenReadStream();
        var (isValid, errorMessage, mimeType) = ImageUploadValidator.ValidateImage(stream, image.Length);
        if (!isValid)
            return BadRequest(new { error = errorMessage });

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var command = new UploadAndAnalyzeMealCommand(
            currentUserId,
            userTier,
            stream,
            image.FileName,
            mimeType!,
            regionalContext,
            mealType
        );

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            confidenceGated = result.Data!.ConfidenceGated,
            confidenceScore = result.Data.ConfidenceScore,
            detectedByModel = result.Data.DetectedByModel,
            photoUrl = result.Data.PhotoUrl,
            message = result.Data.Message,
            advice = result.Data.Advice,
            requiresRetake = result.Data.RequiresRetake,
            analysis = result.Data.Analysis
        });
    }

    [HttpPost("analyze-text")]
    public async Task<IActionResult> AnalyzeTextMeal([FromBody] TextAnalysisRequest? request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (request == null || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description cannot be empty." });

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var command = new AnalyzeTextMealCommand(currentUserId, userTier, request.Description, request.MealType);
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            confidenceGated = result.Data!.ConfidenceGated,
            confidenceScore = result.Data.ConfidenceScore,
            detectedByModel = result.Data.DetectedByModel,
            analysis = result.Data.Analysis
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmMeal([FromBody] MealLog meal, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new ConfirmMealCommand(meal, currentUserId, User.IsAdminOrSuper());
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            meal = result.Data!.Meal,
            dailyLedger = result.Data.DailyLedger,
            learnedNotes = result.Data.LearnedNotes,
            learnedMessage = result.Data.LearnedMessage,
            message = result.Data.Message
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMealById([FromRoute] string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetMealByIdQuery(id, currentUserId, User.IsAdminOrSuper()), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMeal([FromRoute] string id, [FromBody] MealLog meal, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new UpdateMealCommand(id, meal, currentUserId, User.IsAdminOrSuper());
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            meal = result.Data!.Meal,
            dailyLedger = result.Data.DailyLedger,
            message = result.Data.Message
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeal([FromRoute] string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new DeleteMealCommand(id, currentUserId, User.IsAdminOrSuper());
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new
        {
            success = result.Data!.Success,
            message = result.Data.Message
        });
    }

    [HttpGet("corrections")]
    public async Task<IActionResult> GetUserCorrections([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetCorrectionsQuery(currentUserId, userId, User.IsAdminOrSuper());
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("corrections/reset")]
    public async Task<IActionResult> ResetUserCorrections([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new ResetUserCorrectionsCommand(currentUserId, userId, User.IsAdminOrSuper());
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new { message = result.Data });
    }

    [HttpDelete("corrections/{id}")]
    public async Task<IActionResult> DeleteCorrection(string id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var command = new DeleteCorrectionCommand(id, currentUserId, User.IsAdminOrSuper());
        var result = await _dispatcher.SendAsync(command, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(new { message = result.Data });
    }

    [HttpPost("ai-feedback")]
    public async Task<IActionResult> SubmitAiFeedback([FromBody] AiFeedbackRequest request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (request == null)
            return BadRequest(new { error = "Feedback details are required." });

        var command = new SubmitAiFeedbackCommand(
            currentUserId,
            request.UserId,
            User.IsAdminOrSuper(),
            request.MealLogId,
            request.DishName,
            request.DetectedByModel,
            request.ConfidenceScore,
            request.Rating,
            request.Remarks,
            request.Items
        );

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("ai-feedback")]
    public async Task<IActionResult> GetAiFeedbacks([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetAiFeedbacksQuery(currentUserId, userId, User.IsAdminOrSuper());
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("estimate-item")]
    public async Task<IActionResult> EstimateFoodItem([FromBody] FoodItemEstimateRequest request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Name))
            return BadRequest(new { error = "Food item name is required." });

        var query = new EstimateFoodItemQuery(
            currentUserId,
            request.Name,
            request.Portion,
            request.UseAi,
            request.MealType
        );

        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetMealHistory(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        [FromQuery] string? date = null,
        [FromQuery] string? mealType = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var query = new GetMealHistoryQuery(currentUserId, userId, User.IsAdminOrSuper(), period, date, mealType);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportMealHistory(
        [FromQuery] string? userId,
        [FromQuery] string period = "7D",
        [FromQuery] string? date = null,
        [FromQuery] string? mealType = null,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var query = new ExportMealHistoryQuery(
            currentUserId,
            userId,
            userTier,
            User.IsAdminOrSuper(),
            period,
            date,
            mealType
        );

        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return File(result.Data!.FileBytes, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet("quota")]
    public async Task<IActionResult> GetAiQuota(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var tierString = User.GetTier();
        var userTier = Enum.TryParse<UserTier>(tierString, out var parsedTier) ? parsedTier : UserTier.Free;

        var query = new GetAiQuotaQuery(currentUserId, userTier);
        var result = await _dispatcher.QueryAsync(query, ct);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorCode, message = result.Error });
        }

        return Ok(result.Data);
    }
}

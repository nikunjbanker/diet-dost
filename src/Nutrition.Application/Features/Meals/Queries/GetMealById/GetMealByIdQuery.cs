using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.GetMealById;

public record GetMealByIdQuery(
    string Id,
    string CurrentUserId,
    bool IsAdminOrSuper
) : IQuery<Result<MealLog>>;

public class GetMealByIdQueryHandler : IQueryHandler<GetMealByIdQuery, Result<MealLog>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public GetMealByIdQueryHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<MealLog>> HandleAsync(GetMealByIdQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<MealLog>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Id))
            return Result<MealLog>.Failure("Meal id is required.", "MissingId", 400);

        var meal = await _dietitianService.GetMealByIdAsync(request.Id, ct);
        if (meal == null)
            return Result<MealLog>.NotFound($"Meal with ID '{request.Id}' was not found.");

        if (meal.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
            return Result<MealLog>.Forbidden("Access denied to inspect another user's meal.");

        return Result<MealLog>.Success(meal);
    }
}

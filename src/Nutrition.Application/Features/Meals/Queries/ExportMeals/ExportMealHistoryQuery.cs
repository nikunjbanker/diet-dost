using System.Text;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.ExportMeals;

public record ExportMealHistoryQuery(
    string CurrentUserId,
    string? TargetUserId,
    UserTier UserTier,
    bool IsAdminOrSuper,
    string Period,
    string? Date,
    string? MealType
) : IQuery<Result<ExportMealHistoryResultDto>>;

public record ExportMealHistoryResultDto(
    byte[] FileBytes,
    string ContentType,
    string FileName
);

public class ExportMealHistoryQueryHandler : IQueryHandler<ExportMealHistoryQuery, Result<ExportMealHistoryResultDto>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly ITierConfigurationService _tierConfigService;

    public ExportMealHistoryQueryHandler(
        ClinicalDietitianService dietitianService,
        ITierConfigurationService tierConfigService)
    {
        _dietitianService = dietitianService;
        _tierConfigService = tierConfigService;
    }

    public async Task<Result<ExportMealHistoryResultDto>> HandleAsync(ExportMealHistoryQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<ExportMealHistoryResultDto>.Unauthorized();

        var tierConfig = await _tierConfigService.GetConfigurationAsync(request.UserTier, ct);
        if (!tierConfig.AllowDataExport && !request.IsAdminOrSuper)
        {
            return Result<ExportMealHistoryResultDto>.Failure(
                "Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.",
                "FeatureTierUpgradeRequired",
                403);
        }

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var d))
        {
            parsedDate = d;
        }

        MealType? parsedMealType = null;
        if (!string.IsNullOrWhiteSpace(request.MealType) && Enum.TryParse<MealType>(request.MealType, true, out var mt))
        {
            parsedMealType = mt;
        }

        var meals = await _dietitianService.GetMealHistoryAsync(effectiveUserId, request.Period, parsedDate, parsedMealType, ct);

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 Byte Order Mark for Excel
        sb.AppendLine("Meal ID,Date,Time,Meal Type,Dish Name,Calories (kcal),Protein (g),Carbs (g),Fat (g),Fiber (g),Sugar (g),Sodium (mg),Ghee/Tadka (kcal),Food Items Breakdown,AI Confidence,Verified By User,Dietitian Clinical Advice,Feedback Rating,Feedback Remarks");

        static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        foreach (var meal in meals)
        {
            var localTime = meal.LoggedAt.ToLocalTime();
            var dateStr = localTime.ToString("yyyy-MM-dd");
            var timeStr = localTime.ToString("HH:mm:ss");

            var itemsBreakdown = string.Join("; ", meal.Items.Select(i =>
                $"{i.Name} [Portion: {i.EstimatedPortion}, Qty: {i.Quantity}, Kcal: {i.Calories}, P: {i.ProteinGrams}g, C: {i.CarbsGrams}g, F: {i.FatGrams}g, Fib: {i.FiberGrams}g, Sug: {i.SugarGrams}g, Sod: {i.SodiumMg}mg]"));

            var addedCookingFat = Math.Round(meal.AddedGheeKcal + meal.AddedTadkaKcal, 1);

            sb.AppendLine(string.Join(",",
                EscapeCsv(meal.Id),
                EscapeCsv(dateStr),
                EscapeCsv(timeStr),
                EscapeCsv(meal.MealType.ToString()),
                EscapeCsv(meal.DishName),
                meal.TotalCalories.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalProteinGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalCarbsGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalFatGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalFiberGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalSugarGrams.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                meal.TotalSodiumMg.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                addedCookingFat.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                EscapeCsv(itemsBreakdown),
                meal.OverallConfidenceScore.ToString("P0", System.Globalization.CultureInfo.InvariantCulture),
                meal.IsVerifiedByUser ? "Yes" : "No",
                EscapeCsv(meal.DietitianAdvice),
                EscapeCsv(meal.AiFeedbackRating),
                EscapeCsv(meal.AiFeedbackRemarks)
            ));
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"DietDost_Meals_{request.Period}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return Result<ExportMealHistoryResultDto>.Success(new ExportMealHistoryResultDto(
            csvBytes,
            "text/csv; charset=utf-8",
            fileName
        ));
    }
}

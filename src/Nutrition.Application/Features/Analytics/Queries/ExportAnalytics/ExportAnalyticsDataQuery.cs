/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Analytics.Queries.ExportAnalytics;

public record ExportAnalyticsDataQuery(
    string CurrentUserId,
    string? TargetUserId,
    UserTier UserTier,
    bool IsAdminOrSuper
) : IQuery<Result<ExportAnalyticsResultDto>>;

public record ExportAnalyticsResultDto(
    byte[] FileBytes,
    string ContentType,
    string FileName
);

public class ExportAnalyticsDataQueryHandler : IQueryHandler<ExportAnalyticsDataQuery, Result<ExportAnalyticsResultDto>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly ITierConfigurationService _tierConfigService;

    public ExportAnalyticsDataQueryHandler(
        ClinicalDietitianService dietitianService,
        ITierConfigurationService tierConfigService)
    {
        _dietitianService = dietitianService;
        _tierConfigService = tierConfigService;
    }

    public async Task<Result<ExportAnalyticsResultDto>> HandleAsync(ExportAnalyticsDataQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<ExportAnalyticsResultDto>.Unauthorized();

        var config = await _tierConfigService.GetConfigurationAsync(request.UserTier, ct);

        if (!config.AllowDataExport && !request.IsAdminOrSuper)
        {
            return Result<ExportAnalyticsResultDto>.Failure(
                "Data export is a Premium tier feature. Please upgrade your plan.",
                "FeatureTierUpgradeRequired",
                403);
        }

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<ExportAnalyticsResultDto>.Forbidden("Access denied to export another user's data.");
        }

        var meals = await _dietitianService.GetMealHistoryAsync(targetUserId, "365D", null, null, ct);
        var csv = new StringBuilder();
        csv.AppendLine("LoggedAtUtc,MealType,TotalCalories,TotalProteinGrams,TotalCarbsGrams,TotalFatGrams,TotalSodiumMg,ItemCount");
        foreach (var m in meals)
        {
            csv.AppendLine($"{m.LoggedAt:O},{m.MealType},{m.TotalCalories:F1},{m.TotalProteinGrams:F1},{m.TotalCarbsGrams:F1},{m.TotalFatGrams:F1},{m.TotalSodiumMg:F1},{m.Items?.Count ?? 0}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"diet_dost_export_{DateTime.UtcNow:yyyyMMdd}.csv";

        return Result<ExportAnalyticsResultDto>.Success(new ExportAnalyticsResultDto(
            bytes,
            "text/csv",
            fileName
        ));
    }
}

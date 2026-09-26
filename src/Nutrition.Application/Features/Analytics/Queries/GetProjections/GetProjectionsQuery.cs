/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Analytics.Queries.GetProjections;

public record GetProjectionsQuery(
    string CurrentUserId,
    string? TargetUserId,
    UserTier UserTier,
    bool IsAdminOrSuper,
    string Period
) : IQuery<Result<AnalyticsProjection>>;

public class GetProjectionsQueryHandler : IQueryHandler<GetProjectionsQuery, Result<AnalyticsProjection>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly ITierConfigurationService _tierConfigService;

    public GetProjectionsQueryHandler(
        ClinicalDietitianService dietitianService,
        ITierConfigurationService tierConfigService)
    {
        _dietitianService = dietitianService;
        _tierConfigService = tierConfigService;
    }

    public async Task<Result<AnalyticsProjection>> HandleAsync(GetProjectionsQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<AnalyticsProjection>.Unauthorized();

        var config = await _tierConfigService.GetConfigurationAsync(request.UserTier, ct);

        var normPeriod = (request.Period ?? "7D").ToUpperInvariant();
        int requestedDays = normPeriod switch
        {
            "1D" or "DAILY" => 1,
            "7D" or "WEEKLY" => 7,
            "30D" or "MONTHLY" => 30,
            "365D" or "YEARLY" or "1Y" => 365,
            _ => 7
        };

        if (requestedDays > config.AnalyticsHistoryDays && !request.IsAdminOrSuper)
        {
            return Result<AnalyticsProjection>.Failure(
                $"Historical analytics for {normPeriod} requires an upgraded tier (Your plan allows up to {config.AnalyticsHistoryDays} days).",
                "FeatureTierUpgradeRequired",
                403);
        }

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<AnalyticsProjection>.Forbidden("Access denied to inspect another user's projections.");
        }

        var projections = await _dietitianService.GetAnalyticsProjectionAsync(targetUserId, normPeriod, ct);
        return Result<AnalyticsProjection>.Success(projections);
    }
}

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

using Nutrition.Application.Features.Analytics.Queries.GetHistoricalAnalytics;

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
    private readonly IQueryHandler<GetHistoricalAnalyticsQuery, Result<AnalyticsProjection>> _handler;

    public GetProjectionsQueryHandler(IQueryHandler<GetHistoricalAnalyticsQuery, Result<AnalyticsProjection>> handler)
    {
        _handler = handler;
    }

    public Task<Result<AnalyticsProjection>> HandleAsync(GetProjectionsQuery request, CancellationToken ct = default)
    {
        return _handler.HandleAsync(new GetHistoricalAnalyticsQuery(
            request.CurrentUserId,
            request.TargetUserId,
            request.UserTier,
            request.IsAdminOrSuper,
            request.Period), ct);
    }
}

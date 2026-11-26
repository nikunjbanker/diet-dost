/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.GetAiFeedbacks;

public record GetAiFeedbacksQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper
) : IQuery<Result<List<AiDetectionFeedbackDto>>>;

public class GetAiFeedbacksQueryHandler : IQueryHandler<GetAiFeedbacksQuery, Result<List<AiDetectionFeedbackDto>>>
{
    private readonly IRepository<AiDetectionFeedbackRecord> _feedbackRepo;

    public GetAiFeedbacksQueryHandler(IRepository<AiDetectionFeedbackRecord> feedbackRepo)
    {
        _feedbackRepo = feedbackRepo;
    }

    public async Task<Result<List<AiDetectionFeedbackDto>>> HandleAsync(GetAiFeedbacksQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<List<AiDetectionFeedbackDto>>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        var feedbacks = await _feedbackRepo.FindAsync(f => f.UserId == effectiveUserId, ct);
        return Result<List<AiDetectionFeedbackDto>>.Success(
            feedbacks.OrderByDescending(f => f.CreatedAtUtc).Select(f => f.ToDto()).ToList());
    }
}

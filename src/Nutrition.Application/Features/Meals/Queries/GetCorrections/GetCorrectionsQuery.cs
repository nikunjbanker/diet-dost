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
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Queries.GetCorrections;

public record GetCorrectionsQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper
) : IQuery<Result<List<UserCorrectionRecord>>>;

public class GetCorrectionsQueryHandler : IQueryHandler<GetCorrectionsQuery, Result<List<UserCorrectionRecord>>>
{
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;

    public GetCorrectionsQueryHandler(IRepository<UserCorrectionRecord> correctionsRepo)
    {
        _correctionsRepo = correctionsRepo;
    }

    public async Task<Result<List<UserCorrectionRecord>>> HandleAsync(GetCorrectionsQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<List<UserCorrectionRecord>>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        var corrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);
        return Result<List<UserCorrectionRecord>>.Success(corrections.OrderByDescending(c => c.CreatedAtUtc).ToList());
    }
}

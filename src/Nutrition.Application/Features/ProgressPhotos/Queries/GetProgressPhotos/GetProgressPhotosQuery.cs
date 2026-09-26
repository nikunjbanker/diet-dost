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
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.Queries.GetProgressPhotos;

public record GetProgressPhotosQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper,
    ProgressPhotoType? PhotoType
) : IQuery<Result<List<ProgressPhoto>>>;

public class GetProgressPhotosQueryHandler : IQueryHandler<GetProgressPhotosQuery, Result<List<ProgressPhoto>>>
{
    private readonly IRepository<ProgressPhoto> _photoRepo;

    public GetProgressPhotosQueryHandler(IRepository<ProgressPhoto> photoRepo)
    {
        _photoRepo = photoRepo;
    }

    public async Task<Result<List<ProgressPhoto>>> HandleAsync(GetProgressPhotosQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
        {
            return Result<List<ProgressPhoto>>.Unauthorized();
        }

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<List<ProgressPhoto>>.Forbidden();
        }

        var photos = await _photoRepo.FindAsync(
            p => p.UserId == targetUserId && (!request.PhotoType.HasValue || p.PhotoType == request.PhotoType.Value),
            ct);

        var ordered = photos.OrderBy(p => p.CapturedAtUtc).ToList();
        return Result<List<ProgressPhoto>>.Success(ordered);
    }
}

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
using Nutrition.Application.Features.ProgressPhotos.DTOs;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.Queries.CompareProgressPhotos;

public record CompareProgressPhotosQuery(
    string CurrentUserId,
    string? TargetUserId,
    UserTier Tier,
    bool IsAdminOrSuper
) : IQuery<Result<ProgressPhotoComparisonDto>>;

public class CompareProgressPhotosQueryHandler : IQueryHandler<CompareProgressPhotosQuery, Result<ProgressPhotoComparisonDto>>
{
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly ITierConfigurationService _tierConfigService;

    public CompareProgressPhotosQueryHandler(
        IRepository<ProgressPhoto> photoRepo,
        ITierConfigurationService tierConfigService)
    {
        _photoRepo = photoRepo;
        _tierConfigService = tierConfigService;
    }

    public async Task<Result<ProgressPhotoComparisonDto>> HandleAsync(CompareProgressPhotosQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
        {
            return Result<ProgressPhotoComparisonDto>.Unauthorized();
        }

        var config = await _tierConfigService.GetConfigurationAsync(request.Tier, ct);
        if (!config.AllowPhotoCompare && !request.IsAdminOrSuper)
        {
            return Result<ProgressPhotoComparisonDto>.Failure(
                "Visual Photo Comparison is a Premium tier feature. Please upgrade your plan.",
                errorCode: "FeatureTierUpgradeRequired",
                statusCode: 403);
        }

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<ProgressPhotoComparisonDto>.Forbidden();
        }

        var allPhotos = (await _photoRepo.FindAsync(p => p.UserId == targetUserId, ct))
            .OrderBy(p => p.CapturedAtUtc)
            .ToList();

        var facePhotos = allPhotos.Where(p => p.PhotoType == ProgressPhotoType.Face).ToList();
        var fullBodyPhotos = allPhotos.Where(p => p.PhotoType != ProgressPhotoType.Face).ToList();

        var baselineFace = facePhotos.FirstOrDefault(p => p.IsBaseline) ?? facePhotos.FirstOrDefault();
        var currentFace = facePhotos.LastOrDefault(p => p.Id != baselineFace?.Id) ?? baselineFace;

        var baselineFullBody = fullBodyPhotos.FirstOrDefault(p => p.IsBaseline) ?? fullBodyPhotos.FirstOrDefault();
        var currentFullBody = fullBodyPhotos.LastOrDefault(p => p.Id != baselineFullBody?.Id) ?? baselineFullBody;

        double weightDeltaKg = 0;
        int daysElapsed = 0;

        if (baselineFace != null && currentFace != null && baselineFace.Id != currentFace.Id)
        {
            weightDeltaKg = Math.Round(currentFace.WeightKg - baselineFace.WeightKg, 1);
            daysElapsed = Math.Max(1, (int)(currentFace.CapturedAtUtc.Date - baselineFace.CapturedAtUtc.Date).TotalDays);
        }
        else if (baselineFullBody != null && currentFullBody != null && baselineFullBody.Id != currentFullBody.Id)
        {
            weightDeltaKg = Math.Round(currentFullBody.WeightKg - baselineFullBody.WeightKg, 1);
            daysElapsed = Math.Max(1, (int)(currentFullBody.CapturedAtUtc.Date - baselineFullBody.CapturedAtUtc.Date).TotalDays);
        }

        var hasComparison = (baselineFace != null && currentFace != null && baselineFace.Id != currentFace.Id) ||
                            (baselineFullBody != null && currentFullBody != null && baselineFullBody.Id != currentFullBody.Id);

        var dto = new ProgressPhotoComparisonDto(
            hasComparison,
            baselineFace,
            currentFace,
            baselineFullBody,
            currentFullBody,
            weightDeltaKg,
            daysElapsed,
            allPhotos.Count,
            allPhotos);

        return Result<ProgressPhotoComparisonDto>.Success(dto);
    }
}

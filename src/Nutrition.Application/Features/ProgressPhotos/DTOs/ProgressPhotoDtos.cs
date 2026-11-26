/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.DTOs;

public sealed record ProgressPhotoDto(
    string Id,
    string UserId,
    DateTime CapturedAtUtc,
    double WeightKg,
    ProgressPhotoType PhotoType,
    string PhotoUri,
    bool IsBaseline,
    string? Notes
);

public record ProgressPhotoUploadResultDto(
    ProgressPhotoDto Photo,
    string Message
);

public record ProgressPhotoComparisonDto(
    bool HasComparison,
    ProgressPhotoDto? BaselineFace,
    ProgressPhotoDto? CurrentFace,
    ProgressPhotoDto? BaselineFullBody,
    ProgressPhotoDto? CurrentFullBody,
    double WeightDeltaKg,
    int DaysElapsed,
    int TotalPhotosCount,
    IReadOnlyList<ProgressPhotoDto> AllPhotos
);

public static class ProgressPhotoDtoMapper
{
    public static ProgressPhotoDto ToDto(this ProgressPhoto entity) => new(
        Id: entity.Id,
        UserId: entity.UserId,
        CapturedAtUtc: entity.CapturedAtUtc,
        WeightKg: entity.WeightKg,
        PhotoType: entity.PhotoType,
        PhotoUri: $"/api/media/progress/{entity.Id}",
        IsBaseline: entity.IsBaseline,
        Notes: entity.Notes
    );
}

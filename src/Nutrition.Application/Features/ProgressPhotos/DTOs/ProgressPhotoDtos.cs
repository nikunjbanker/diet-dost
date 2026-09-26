/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.DTOs;

public record ProgressPhotoUploadResultDto(
    ProgressPhoto Photo,
    string Message
);

public record ProgressPhotoComparisonDto(
    bool HasComparison,
    ProgressPhoto? BaselineFace,
    ProgressPhoto? CurrentFace,
    ProgressPhoto? BaselineFullBody,
    ProgressPhoto? CurrentFullBody,
    double WeightDeltaKg,
    int DaysElapsed,
    int TotalPhotosCount,
    IReadOnlyList<ProgressPhoto> AllPhotos
);

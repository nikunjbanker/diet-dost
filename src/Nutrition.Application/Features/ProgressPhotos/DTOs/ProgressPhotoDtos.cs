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

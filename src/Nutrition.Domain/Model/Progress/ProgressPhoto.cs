/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Progress;

public enum ProgressPhotoType
{
    Face,
    FullBodyFront,
    FullBodySide
}

public class ProgressPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public double WeightKg { get; set; }
    public ProgressPhotoType PhotoType { get; set; } = ProgressPhotoType.Face;
    public string PhotoUri { get; set; } = string.Empty;
    public bool IsBaseline { get; set; }
    public string? Notes { get; set; }
}

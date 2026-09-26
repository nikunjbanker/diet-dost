/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Port abstraction for photo persistence (local disk, cloud storage bucket),
/// completely decoupling controllers and domain handlers from direct file system I/O.
/// </summary>
public interface IPhotoStorageService
{
    Task<string> SaveMealPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
    Task<string> SaveProgressPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);
    Task<bool> DeletePhotoAsync(string relativePathOrUrl, CancellationToken ct = default);
}

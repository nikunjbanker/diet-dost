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

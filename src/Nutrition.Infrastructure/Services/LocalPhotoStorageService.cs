using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter implementing IPhotoStorageService for local disk / wwwroot persistence.
/// Pure BCL implementation decoupled from ASP.NET Core presentation contracts.
/// </summary>
public class LocalPhotoStorageService : IPhotoStorageService
{
    private readonly string _webRootPath;
    private readonly ILogger<LocalPhotoStorageService> _logger;

    public LocalPhotoStorageService(IConfiguration configuration, ILogger<LocalPhotoStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _webRootPath = configuration?["Storage:WebRootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> SaveMealPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SavePhotoInternalAsync("meals", fileStream, originalFileName, contentType, ct);
    }

    public async Task<string> SaveProgressPhotoAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        return await SavePhotoInternalAsync("progress", fileStream, originalFileName, contentType, ct);
    }

    public Task<bool> DeletePhotoAsync(string relativePathOrUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePathOrUrl))
            return Task.FromResult(false);

        try
        {
            var trimmedPath = relativePathOrUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_webRootPath, trimmedPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete photo at path: {Path}", relativePathOrUrl);
            return Task.FromResult(false);
        }
    }

    private async Task<string> SavePhotoInternalAsync(
        string subFolder,
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var destinationFolder = Path.Combine(_webRootPath, "uploads", subFolder);
        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var uniqueFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(destinationFolder, uniqueFileName);

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using var fileOutput = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(fileOutput, ct);

        return $"/uploads/{subFolder}/{uniqueFileName}";
    }
}

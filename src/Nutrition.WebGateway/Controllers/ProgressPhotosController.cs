using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Controllers;

[ApiController]
[Route("api/progress-photos")]
public class ProgressPhotosController : ControllerBase
{
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProgressPhotosController> _logger;

    public ProgressPhotosController(
        IRepository<ProgressPhoto> photoRepo,
        IUnitOfWork uow,
        IWebHostEnvironment env,
        ILogger<ProgressPhotosController> logger)
    {
        _photoRepo = photoRepo;
        _uow = uow;
        _env = env;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ImageUploadValidator.MaxSizeBytes)]
    public async Task<IActionResult> UploadProgressPhoto(
        [FromForm] IFormFile? image,
        [FromForm] string userId,
        [FromForm] double? weightKg,
        [FromForm] ProgressPhotoType photoType = ProgressPhotoType.Face,
        [FromForm] bool isBaseline = false,
        [FromForm] string? notes = null,
        [FromForm] string? capturedDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Please provide an image file." });

        using var stream = image.OpenReadStream();
        var (isValid, errorMessage, mimeType) = ImageUploadValidator.ValidateImage(stream, image.Length);
        if (!isValid)
            return BadRequest(new { error = errorMessage });

        // Ensure storage directory exists
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "progress");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var extension = mimeType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        var fileName = $"{userId}_{photoType}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        stream.Position = 0;
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        var relativeUri = $"/uploads/progress/{fileName}";

        var capturedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(capturedDate) && DateTime.TryParse(capturedDate, out var parsedDate))
        {
            capturedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
        }

        var photo = new ProgressPhoto
        {
            UserId = userId,
            CapturedAtUtc = capturedAt,
            WeightKg = weightKg ?? 80.0,
            PhotoType = photoType,
            PhotoUri = relativeUri,
            IsBaseline = isBaseline,
            Notes = notes?.Trim()
        };

        await _photoRepo.AddAsync(photo, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saved new progress photo {Id} for user {UserId}, type: {Type}", photo.Id, userId, photoType);

        return Ok(new
        {
            photo,
            message = isBaseline
                ? "🌟 Baseline progress photo successfully recorded!"
                : "📸 Progress check-in photo saved! Visual timeline updated."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetPhotos(
        [FromQuery] string userId,
        [FromQuery] ProgressPhotoType? photoType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var photos = await _photoRepo.FindAsync(
            p => p.UserId == userId && (!photoType.HasValue || p.PhotoType == photoType.Value),
            ct);

        var ordered = photos.OrderBy(p => p.CapturedAtUtc).ToList();
        return Ok(ordered);
    }

    [HttpGet("comparison")]
    public async Task<IActionResult> GetComparison(
        [FromQuery] string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var allPhotos = (await _photoRepo.FindAsync(p => p.UserId == userId, ct))
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

        return Ok(new
        {
            hasComparison = (baselineFace != null && currentFace != null && baselineFace.Id != currentFace.Id) ||
                            (baselineFullBody != null && currentFullBody != null && baselineFullBody.Id != currentFullBody.Id),
            baselineFace,
            currentFace,
            baselineFullBody,
            currentFullBody,
            weightDeltaKg,
            daysElapsed,
            totalPhotosCount = allPhotos.Count,
            allPhotos
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePhoto(string id, CancellationToken ct = default)
    {
        var photo = await _photoRepo.GetByIdAsync(id, ct);
        if (photo == null)
            return NotFound(new { error = "Photo not found." });

        // Try delete physical file
        try
        {
            var relative = photo.PhotoUri.TrimStart('/');
            var fullPath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), relative);
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete physical photo file {Uri}", photo.PhotoUri);
        }

        await _photoRepo.DeleteAsync(photo.Id, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(new { message = "Progress photo deleted successfully." });
    }
}

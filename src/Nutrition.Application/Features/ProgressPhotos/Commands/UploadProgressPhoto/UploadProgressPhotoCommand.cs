/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.ProgressPhotos.DTOs;
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.Commands.UploadProgressPhoto;

public record UploadProgressPhotoCommand(
    string UserId,
    Stream ImageStream,
    string FileName,
    string MimeType,
    double? WeightKg,
    ProgressPhotoType PhotoType,
    bool IsBaseline,
    string? Notes,
    string? CapturedDate
) : ICommand<Result<ProgressPhotoUploadResultDto>>;

public class UploadProgressPhotoCommandHandler : ICommandHandler<UploadProgressPhotoCommand, Result<ProgressPhotoUploadResultDto>>
{
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IPhotoStorageService _photoStorageService;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UploadProgressPhotoCommandHandler> _logger;

    public UploadProgressPhotoCommandHandler(
        IRepository<ProgressPhoto> photoRepo,
        IPhotoStorageService photoStorageService,
        IUnitOfWork uow,
        ILogger<UploadProgressPhotoCommandHandler> logger)
    {
        _photoRepo = photoRepo;
        _photoStorageService = photoStorageService;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<ProgressPhotoUploadResultDto>> HandleAsync(UploadProgressPhotoCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return Result<ProgressPhotoUploadResultDto>.Unauthorized();
        }

        var photoUri = await _photoStorageService.SaveProgressPhotoAsync(
            request.ImageStream,
            request.FileName,
            request.MimeType,
            ct);

        var capturedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.CapturedDate) && DateTime.TryParse(request.CapturedDate, out var parsedDate))
        {
            capturedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
        }

        var photo = new ProgressPhoto
        {
            UserId = request.UserId,
            CapturedAtUtc = capturedAt,
            WeightKg = request.WeightKg ?? 80.0,
            PhotoType = request.PhotoType,
            PhotoUri = photoUri,
            IsBaseline = request.IsBaseline,
            Notes = request.Notes?.Trim()
        };

        await _photoRepo.AddAsync(photo, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saved new progress photo {Id} for user {UserId}, type: {Type}", photo.Id, request.UserId, request.PhotoType);

        var message = request.IsBaseline
            ? "🌟 Baseline progress photo successfully recorded!"
            : "📸 Progress check-in photo saved! Visual timeline updated.";

        return Result<ProgressPhotoUploadResultDto>.Success(new ProgressPhotoUploadResultDto(photo, message));
    }
}

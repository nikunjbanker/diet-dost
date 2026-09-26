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
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.ProgressPhotos.Commands.DeleteProgressPhoto;

public record DeleteProgressPhotoCommand(
    string PhotoId,
    string CurrentUserId,
    bool IsAdminOrSuper
) : ICommand<Result>;

public class DeleteProgressPhotoCommandHandler : ICommandHandler<DeleteProgressPhotoCommand, Result>
{
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IPhotoStorageService _photoStorageService;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DeleteProgressPhotoCommandHandler> _logger;

    public DeleteProgressPhotoCommandHandler(
        IRepository<ProgressPhoto> photoRepo,
        IPhotoStorageService photoStorageService,
        IUnitOfWork uow,
        ILogger<DeleteProgressPhotoCommandHandler> logger)
    {
        _photoRepo = photoRepo;
        _photoStorageService = photoStorageService;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(DeleteProgressPhotoCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
        {
            return Result.Unauthorized();
        }

        var photo = await _photoRepo.GetByIdAsync(request.PhotoId, ct);
        if (photo == null)
        {
            return Result.NotFound("Photo not found.");
        }

        if (photo.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result.Forbidden();
        }

        try
        {
            await _photoStorageService.DeletePhotoAsync(photo.PhotoUri, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete physical photo file {Uri}", photo.PhotoUri);
        }

        await _photoRepo.DeleteAsync(photo.Id, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted progress photo {PhotoId} for user {UserId}", photo.Id, photo.UserId);

        return Result.Ok();
    }
}

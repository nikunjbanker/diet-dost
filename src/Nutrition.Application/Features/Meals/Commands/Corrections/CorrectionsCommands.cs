/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Domain.Model.Meal;

namespace Nutrition.Application.Features.Meals.Commands.Corrections;

public record ResetUserCorrectionsCommand(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper
) : ICommand<Result<string>>;

public class ResetUserCorrectionsCommandHandler : ICommandHandler<ResetUserCorrectionsCommand, Result<string>>
{
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IUnitOfWork _uow;

    public ResetUserCorrectionsCommandHandler(IRepository<UserCorrectionRecord> correctionsRepo, IUnitOfWork uow)
    {
        _correctionsRepo = correctionsRepo;
        _uow = uow;
    }

    public async Task<Result<string>> HandleAsync(ResetUserCorrectionsCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<string>.Unauthorized();

        var effectiveUserId = (!string.IsNullOrWhiteSpace(request.TargetUserId) && request.IsAdminOrSuper)
            ? request.TargetUserId
            : request.CurrentUserId;

        var corrections = await _correctionsRepo.FindAsync(c => c.UserId == effectiveUserId, ct);
        foreach (var c in corrections)
        {
            await _correctionsRepo.DeleteAsync(c.Id, ct);
        }
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success($"Cleared {corrections.Count} trained memory corrections for user {effectiveUserId}.");
    }
}

public record DeleteCorrectionCommand(
    string Id,
    string CurrentUserId,
    bool IsAdminOrSuper
) : ICommand<Result<string>>;

public class DeleteCorrectionCommandHandler : ICommandHandler<DeleteCorrectionCommand, Result<string>>
{
    private readonly IRepository<UserCorrectionRecord> _correctionsRepo;
    private readonly IUnitOfWork _uow;

    public DeleteCorrectionCommandHandler(IRepository<UserCorrectionRecord> correctionsRepo, IUnitOfWork uow)
    {
        _correctionsRepo = correctionsRepo;
        _uow = uow;
    }

    public async Task<Result<string>> HandleAsync(DeleteCorrectionCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<string>.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Id))
            return Result<string>.Failure("Id is required.", "MissingId", 400);

        var existing = await _correctionsRepo.GetByIdAsync(request.Id, ct);
        if (existing == null)
            return Result<string>.NotFound("Correction not found.");

        if (existing.UserId != request.CurrentUserId && !request.IsAdminOrSuper)
            return Result<string>.Forbidden("Access denied to delete another user's correction.");

        await _correctionsRepo.DeleteAsync(request.Id, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<string>.Success("Correction deleted successfully.");
    }
}

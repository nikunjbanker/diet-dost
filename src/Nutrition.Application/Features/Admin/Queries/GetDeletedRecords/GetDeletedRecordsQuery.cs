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
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Progress;

namespace Nutrition.Application.Features.Admin.Queries.GetDeletedRecords;

public record DeletedRecordDto(
    string Id,
    string RecordType,
    string UserId,
    string Title,
    string? PhotoUri,
    DateTime? DeletedAtUtc,
    DateTime? CreatedAtUtc,
    string? ExtraDetails
);

public record GetDeletedRecordsQuery(
    string? RecordType = "all",
    string? UserId = null
) : IQuery<Result<List<DeletedRecordDto>>>;

public class GetDeletedRecordsQueryHandler : IQueryHandler<GetDeletedRecordsQuery, Result<List<DeletedRecordDto>>>
{
    private readonly IRepository<MealLog> _mealRepo;
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IRepository<ApplicationUser> _userRepo;

    public GetDeletedRecordsQueryHandler(
        IRepository<MealLog> mealRepo,
        IRepository<ProgressPhoto> photoRepo,
        IRepository<ApplicationUser> userRepo)
    {
        _mealRepo = mealRepo;
        _photoRepo = photoRepo;
        _userRepo = userRepo;
    }

    public async Task<Result<List<DeletedRecordDto>>> HandleAsync(GetDeletedRecordsQuery request, CancellationToken ct = default)
    {
        var records = new List<DeletedRecordDto>();
        var type = request.RecordType?.ToLowerInvariant() ?? "all";

        // 1. Soft-deleted Meals
        if (type == "all" || type == "meals" || type == "meal")
        {
            var deletedMeals = await _mealRepo.FindDeletedAsync(
                m => string.IsNullOrWhiteSpace(request.UserId) || m.UserId == request.UserId,
                ct);

            records.AddRange(deletedMeals
                .OrderByDescending(m => m.DeletedAtUtc)
                .Take(100)
                .Select(m => new DeletedRecordDto(
                    m.Id,
                    "Meal",
                    m.UserId,
                    !string.IsNullOrWhiteSpace(m.DishName) ? m.DishName : "Meal",
                    m.PhotoUri,
                    m.DeletedAtUtc,
                    m.LoggedAt,
                    $"{m.TotalCalories:F0} kcal, {m.MealType}"
                )));
        }

        // 2. Soft-deleted Progress Photos
        if (type == "all" || type == "progress" || type == "photos")
        {
            var deletedPhotos = await _photoRepo.FindDeletedAsync(
                p => string.IsNullOrWhiteSpace(request.UserId) || p.UserId == request.UserId,
                ct);

            records.AddRange(deletedPhotos
                .OrderByDescending(p => p.DeletedAtUtc)
                .Take(100)
                .Select(p => new DeletedRecordDto(
                    p.Id,
                    "ProgressPhoto",
                    p.UserId,
                    $"{p.PhotoType} Progress Photo",
                    p.PhotoUri,
                    p.DeletedAtUtc,
                    p.CapturedAtUtc,
                    $"{p.WeightKg:F1} kg {(p.IsBaseline ? "(Baseline)" : "")}"
                )));
        }

        // 3. Soft-deleted Users
        if (type == "all" || type == "users" || type == "user")
        {
            var deletedUsers = await _userRepo.FindDeletedAsync(
                u => string.IsNullOrWhiteSpace(request.UserId) || u.Id == request.UserId,
                ct);

            records.AddRange(deletedUsers
                .OrderByDescending(u => u.DeletedAtUtc)
                .Take(100)
                .Select(u => new DeletedRecordDto(
                    u.Id,
                    "User",
                    u.Id,
                    u.Email,
                    null,
                    u.DeletedAtUtc,
                    u.CreatedAtUtc,
                    $"Role: {u.Role}, Tier: {u.Tier}"
                )));
        }

        return Result<List<DeletedRecordDto>>.Success(
            records.OrderByDescending(r => r.DeletedAtUtc ?? DateTime.MinValue).ToList());
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.Analytics.DTOs;
using Nutrition.Application.Services;

namespace Nutrition.Application.Features.Analytics.Queries.GetDailyLedger;

public record GetDailyLedgerQuery(
    string CurrentUserId,
    string? TargetUserId,
    bool IsAdminOrSuper,
    string? Date
) : IQuery<Result<DailyCalorieLedgerDto>>;

public class GetDailyLedgerQueryHandler : IQueryHandler<GetDailyLedgerQuery, Result<DailyCalorieLedgerDto>>
{
    private readonly ClinicalDietitianService _dietitianService;

    public GetDailyLedgerQueryHandler(ClinicalDietitianService dietitianService)
    {
        _dietitianService = dietitianService;
    }

    public async Task<Result<DailyCalorieLedgerDto>> HandleAsync(GetDailyLedgerQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<DailyCalorieLedgerDto>.Unauthorized();

        var targetUserId = string.IsNullOrWhiteSpace(request.TargetUserId) ? request.CurrentUserId : request.TargetUserId;
        if (targetUserId != request.CurrentUserId && !request.IsAdminOrSuper)
        {
            return Result<DailyCalorieLedgerDto>.Forbidden("Access denied to inspect another user's ledger.");
        }

        var dateOnly = !string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var ledger = await _dietitianService.GetOrCreateDailyLedgerAsync(targetUserId, dateOnly, ct);
        return Result<DailyCalorieLedgerDto>.Success(ledger.ToDto());
    }
}

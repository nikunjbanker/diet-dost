/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;

namespace Nutrition.Application.Features.SystemWarmup.Queries.WarmupDatabase;

/// <summary>
/// Pre-warms the active database instance (e.g. waking Azure SQL Serverless from cold pause).
/// Safe to be invoked anonymously by clients upon initial app boot, auth gate interaction, or keep-alive pings.
/// </summary>
public record WarmupDatabaseQuery : IQuery<Result<DatabaseWarmupResult>>;

public class WarmupDatabaseQueryHandler : IQueryHandler<WarmupDatabaseQuery, Result<DatabaseWarmupResult>>
{
    private readonly IDatabaseWarmupService _warmupService;

    public WarmupDatabaseQueryHandler(IDatabaseWarmupService warmupService)
    {
        _warmupService = warmupService ?? throw new ArgumentNullException(nameof(warmupService));
    }

    public async Task<Result<DatabaseWarmupResult>> HandleAsync(WarmupDatabaseQuery request, CancellationToken ct = default)
    {
        var result = await _warmupService.PingAndWarmupAsync(ct);
        return Result<DatabaseWarmupResult>.Success(result);
    }
}

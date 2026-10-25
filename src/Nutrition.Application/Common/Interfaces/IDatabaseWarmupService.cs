/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Pre-warms serverless and cloud database instances (such as Azure SQL Serverless Free Tier)
/// by executing a lightweight verification probe to resume cold-paused databases before user operations.
/// </summary>
public interface IDatabaseWarmupService
{
    Task<DatabaseWarmupResult> PingAndWarmupAsync(CancellationToken ct = default);
}

public sealed record DatabaseWarmupResult(
    bool IsOnline,
    string DatabaseProvider,
    long LatencyMs,
    string Message
);

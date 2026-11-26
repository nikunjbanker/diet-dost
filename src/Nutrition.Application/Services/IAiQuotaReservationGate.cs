/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Services;

/// <summary>
/// Per-user concurrency gate preventing AI quota TOCTOU races during concurrent AI meal analysis requests.
/// </summary>
public interface IAiQuotaReservationGate
{
    Task<IAsyncDisposable> AcquireUserLockAsync(string userId, CancellationToken ct = default);
}

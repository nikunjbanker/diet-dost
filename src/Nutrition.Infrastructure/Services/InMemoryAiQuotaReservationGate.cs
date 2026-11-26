/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Collections.Concurrent;
using Nutrition.Application.Services;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Single-replica per-user SemaphoreSlim concurrency gate preventing AI quota TOCTOU races.
/// Behind IAiQuotaReservationGate so multi-replica deployments can swap in a DocumentDB lease.
/// </summary>
public sealed class InMemoryAiQuotaReservationGate : IAiQuotaReservationGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> AcquireUserLockAsync(string userId, CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(userId) ? "_anonymous" : userId;
        var semaphore = _userLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}

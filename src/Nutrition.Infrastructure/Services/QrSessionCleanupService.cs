/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Background worker service executing periodic TTL cleanup of expired QR login sessions,
/// preventing unbounded table growth and database resource exhaustion (GAP-41).
/// </summary>
public class QrSessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QrSessionCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public QrSessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<QrSessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[QR_CLEANUP] QrSessionCleanupService background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[QR_CLEANUP] Error occurred during expired QR session cleanup.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[QR_CLEANUP] QrSessionCleanupService background worker stopped.");
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        // Purge sessions expired more than 24 hours ago
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var expiredSessions = await db.QrSessions
            .Where(s => s.ExpiresAtUtc < cutoff)
            .ToListAsync(ct);

        if (expiredSessions.Count > 0)
        {
            db.QrSessions.RemoveRange(expiredSessions);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("[QR_CLEANUP] Purged {Count} expired QR login sessions older than 24h.", expiredSessions.Count);
        }
    }
}

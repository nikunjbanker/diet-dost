/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.WebGateway.Services;

/// <summary>
/// Hosted background worker that:
/// 1. Flushes debounced in-memory user presence updates to the database repository every 30 seconds.
/// 2. Emits an anonymized OpenTelemetry observable metric gauge ("dietdost.users.active_count")
///    to Azure Monitor Application Insights and Aspire Developer Dashboard with strictly ZERO PII.
/// </summary>
public class AzureMonitorPresenceMetricsWorker : BackgroundService
{
    private readonly IUserPresenceTracker _presenceTracker;
    private readonly ILogger<AzureMonitorPresenceMetricsWorker> _logger;
    private readonly ObservableGauge<int> _activeUsersGauge;

    public AzureMonitorPresenceMetricsWorker(
        IUserPresenceTracker presenceTracker,
        ILogger<AzureMonitorPresenceMetricsWorker> logger)
    {
        _presenceTracker = presenceTracker;
        _logger = logger;

        _activeUsersGauge = NutritionTelemetry.Meter.CreateObservableGauge(
            "dietdost.users.active_count",
            ObserveActiveUserMetrics,
            unit: "{users}",
            description: "Count of actively interacting users over a 15-minute sliding window (aggregated by tier, zero PII)");
    }

    private IEnumerable<Measurement<int>> ObserveActiveUserMetrics()
    {
        var tierCounts = _presenceTracker.GetActiveCountByTier(TimeSpan.FromMinutes(15));
        var totalActive = 0;

        foreach (var (tier, count) in tierCounts)
        {
            totalActive += count;
            yield return new Measurement<int>(
                count,
                new KeyValuePair<string, object?>("tier", tier.ToString()),
                new KeyValuePair<string, object?>("platform", "All"));
        }

        yield return new Measurement<int>(
            totalActive,
            new KeyValuePair<string, object?>("tier", "Total"),
            new KeyValuePair<string, object?>("platform", "All"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AzureMonitorPresenceMetricsWorker started successfully.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await _presenceTracker.FlushDebouncedUpdatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error occurred during debounced presence flush cycle.");
            }
        }

        // Final graceful flush on shutdown
        try
        {
            await _presenceTracker.FlushDebouncedUpdatesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed final graceful presence flush on shutdown.");
        }
    }
}

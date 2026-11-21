/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Infrastructure.Persistence.DocumentDb;

/// <summary>
/// Strongly-typed configuration options for Azure DocumentDB (Free Tier - 32 GB Dedicated Storage).
/// </summary>
public sealed class DocumentDbOptions
{
    public const string SectionName = "DocumentDb";

    /// <summary>
    /// The target database name inside the Azure DocumentDB cluster. Default is "dietdost".
    /// </summary>
    public string DatabaseName { get; set; } = "dietdost";

    /// <summary>
    /// Connection timeout in milliseconds. Default is 60,000ms (60s) to absorb 60-day dormancy cold resumes.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Server selection timeout in milliseconds. Default is 60,000ms (60s).
    /// </summary>
    public int ServerSelectionTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Maximum idle connection time in milliseconds. Default is 120,000ms (2 minutes).
    /// </summary>
    public int MaxConnectionIdleTimeMs { get; set; } = 120000;

    /// <summary>
    /// Retry attempts on transient network or cold-wake cluster failures. Default is 5.
    /// </summary>
    public int RetryAttempts { get; set; } = 5;

    /// <summary>
    /// Allocated dedicated free tier storage capacity in gigabytes (Azure DocumentDB Free Tier offers 32 GB free for life).
    /// </summary>
    public int AllocatedStorageGb { get; set; } = 32;

    /// <summary>
    /// Enables resilient automatic connection retries when resuming from Azure DocumentDB Free Tier 60-day auto-pause.
    /// </summary>
    public bool AutoPauseResilienceEnabled { get; set; } = true;
}

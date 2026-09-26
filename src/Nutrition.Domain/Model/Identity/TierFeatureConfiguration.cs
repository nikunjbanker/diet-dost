/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Dynamic configuration for user subscription tiers.
/// Allows administrators to tune feature limits and quotas at runtime without redeployment.
/// </summary>
public class TierFeatureConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The subscription tier this configuration applies to.
    /// </summary>
    public UserTier Tier { get; set; }

    /// <summary>
    /// Daily ceiling for AI meal detections (photo or text).
    /// -1 denotes unlimited access.
    /// </summary>
    public int DailyAiDetectionLimit { get; set; }

    /// <summary>
    /// Whether visual face/body transformation photo comparison is enabled.
    /// </summary>
    public bool AllowPhotoCompare { get; set; }

    /// <summary>
    /// Whether exporting dietary logs to Excel (.xlsx) / CSV is enabled.
    /// </summary>
    public bool AllowDataExport { get; set; }

    /// <summary>
    /// Maximum days of historical analytics and trends accessible in charts.
    /// </summary>
    public int AnalyticsHistoryDays { get; set; }

    /// <summary>
    /// Human-readable description of this tier's entitlements.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }

    /// <summary>
    /// Returns the standard default tier configurations per system specification.
    /// </summary>
    public static List<TierFeatureConfiguration> GetDefaultConfigurations()
    {
        var now = DateTime.UtcNow;
        return new List<TierFeatureConfiguration>
        {
            new()
            {
                Id = "tier-free",
                Tier = UserTier.Free,
                DailyAiDetectionLimit = 1,
                AllowPhotoCompare = false,
                AllowDataExport = false,
                AnalyticsHistoryDays = 7,
                Description = "Free Tier: 1 AI detection/day, manual logging, 7-day history.",
                UpdatedAtUtc = now
            },
            new()
            {
                Id = "tier-basic",
                Tier = UserTier.Basic,
                DailyAiDetectionLimit = 7,
                AllowPhotoCompare = false,
                AllowDataExport = false,
                AnalyticsHistoryDays = 30,
                Description = "Basic Tier: 7 AI detections/day, 30-day analytics history.",
                UpdatedAtUtc = now
            },
            new()
            {
                Id = "tier-premium",
                Tier = UserTier.Premium,
                DailyAiDetectionLimit = 30,
                AllowPhotoCompare = true,
                AllowDataExport = true,
                AnalyticsHistoryDays = 365,
                Description = "Premium Tier: 30 AI detections/day, visual photo compare, Excel export, 365-day history.",
                UpdatedAtUtc = now
            },
            new()
            {
                Id = "tier-superadmin",
                Tier = UserTier.SuperAdmin,
                DailyAiDetectionLimit = -1,
                AllowPhotoCompare = true,
                AllowDataExport = true,
                AnalyticsHistoryDays = 365,
                Description = "SuperAdmin / God User: Unlimited AI detections, all features enabled, user & tier administration.",
                UpdatedAtUtc = now
            }
        };
    }

    /// <summary>
    /// Returns the default tier configuration for a specific tier.
    /// </summary>
    public static TierFeatureConfiguration CreateDefault(UserTier tier)
    {
        return GetDefaultConfigurations().FirstOrDefault(c => c.Tier == tier) ?? new TierFeatureConfiguration
        {
            Id = $"tier-{tier.ToString().ToLowerInvariant()}",
            Tier = tier,
            DailyAiDetectionLimit = tier == UserTier.SuperAdmin ? -1 : 1,
            AllowPhotoCompare = tier is UserTier.Premium or UserTier.SuperAdmin,
            AllowDataExport = tier is UserTier.Premium or UserTier.SuperAdmin,
            AnalyticsHistoryDays = tier is UserTier.Premium or UserTier.SuperAdmin ? 365 : 7,
            Description = $"{tier} Tier Configuration"
        };
    }
}

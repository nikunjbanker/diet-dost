/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Services;

/// <summary>
/// Domain service interface for dynamic, database-backed tier configuration governance.
/// </summary>
public interface ITierConfigurationService
{
    Task<TierFeatureConfiguration> GetConfigurationAsync(UserTier tier, CancellationToken ct = default);
    Task<List<TierFeatureConfiguration>> GetAllConfigurationsAsync(CancellationToken ct = default);
    Task<TierFeatureConfiguration> UpdateConfigurationAsync(TierFeatureConfiguration configuration, CancellationToken ct = default);
}

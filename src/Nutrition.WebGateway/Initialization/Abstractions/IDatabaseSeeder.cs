/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

namespace Nutrition.WebGateway.Initialization.Abstractions;

/// <summary>
/// Defines an ordered data seeder executed during application initialization.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Execution order (lower runs first).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes the seeding logic.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

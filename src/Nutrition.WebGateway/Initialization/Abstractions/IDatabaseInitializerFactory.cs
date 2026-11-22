/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

namespace Nutrition.WebGateway.Initialization.Abstractions;

/// <summary>
/// Factory interface for resolving the active database initializer strategy matching the configured provider.
/// </summary>
public interface IDatabaseInitializerFactory
{
    /// <summary>
    /// Resolves the strategy for the configured or requested provider.
    /// </summary>
    IDatabaseInitializer GetInitializer(string? providerName = null);
}

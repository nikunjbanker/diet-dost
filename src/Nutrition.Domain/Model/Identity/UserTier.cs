/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Defines user subscription and capability tiers.
/// </summary>
public enum UserTier
{
    Free = 0,
    Basic = 1,
    Premium = 2,
    SuperAdmin = 3
}

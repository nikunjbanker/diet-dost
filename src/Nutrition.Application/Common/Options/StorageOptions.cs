/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Options;

/// <summary>
/// Strongly-typed Options for media and photo filesystem storage paths.
/// Bound to configuration section "Storage".
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string WebRootPath { get; set; } = string.Empty;
}

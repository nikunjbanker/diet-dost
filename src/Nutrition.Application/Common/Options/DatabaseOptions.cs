/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Options;

/// <summary>
/// Strongly-typed Options for Database persistence provider and connection strings.
/// Bound to configuration section "Database" and connection strings.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "Sqlite";
    public string ConnectionString { get; set; } = "Data Source=diettracker.db";
}

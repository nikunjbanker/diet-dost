/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Common;

/// <summary>
/// Domain contract for soft-deletable entities.
/// Prevents irreversible physical data loss, preserves forensic audit history,
/// and enables SuperAdmin data inspection and restoration.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indicates whether this record has been logically deleted.
    /// Excluded from standard application queries via EF Core Global Query Filters.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp in UTC when the record was soft-deleted.
    /// </summary>
    DateTime? DeletedAtUtc { get; set; }
}

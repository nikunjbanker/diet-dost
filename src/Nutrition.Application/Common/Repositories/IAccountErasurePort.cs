/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Repositories;

/// <summary>
/// Port for DPDPA 2023 self-service account erasure across all user-linked persistence collections.
/// </summary>
public interface IAccountErasurePort
{
    Task EraseUserAccountAsync(string userId, DateTime erasedAtUtc, CancellationToken ct = default);
}

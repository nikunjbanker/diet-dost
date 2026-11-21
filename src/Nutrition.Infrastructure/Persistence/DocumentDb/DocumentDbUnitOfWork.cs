/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Persistence.DocumentDb;

/// <summary>
/// Unit of work implementation for Azure DocumentDB. In DocumentDB standalone/free-tier clusters,
/// document replace and insert operations in DocumentDbRepository commit immediately.
/// </summary>
public class DocumentDbUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(1);
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Common.Repositories;

/// <summary>
/// Specialized repository port for atomic Compare-And-Swap state transitions on QrLoginSession.
/// </summary>
public interface IQrLoginSessionRepository
{
    Task<QrLoginSession?> TryMarkScannedAsync(
        string sessionId,
        DateTime utcNow,
        CancellationToken ct = default);

    Task<QrLoginSession?> TryApproveAsync(
        string sessionId,
        string userId,
        string exchangeTicket,
        string? mobilePublicKey,
        string? encryptedApprovalPayload,
        DateTime utcNow,
        CancellationToken ct = default);

    Task<QrLoginSession?> TryConsumeAsync(
        string sessionId,
        string exchangeTicket,
        DateTime utcNow,
        CancellationToken ct = default);

    Task<QrLoginSession?> TryRejectAsync(
        string sessionId,
        DateTime utcNow,
        CancellationToken ct = default);
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Model.Identity;
using Xunit;

namespace Nutrition.Domain.Tests;

public class QrLoginSessionTests
{
    [Fact]
    public void Lifecycle_PendingToScannedToApprovedToConsumed_Succeeds()
    {
        var session = QrLoginSession.CreateNew(
            challengeNonce: "nonce-12345",
            browserSecretHash: "secret-hash-67890",
            confirmationCode: "73",
            confirmationGlyph: "Emerald Falcon",
            ttlSeconds: 90,
            desktopPublicKey: "pk_desktop_test"
        );

        Assert.Equal(QrSessionStatus.Pending, session.Status);
        Assert.False(session.IsExpired);
        Assert.Equal("73", session.ConfirmationCode);
        Assert.Equal("Emerald Falcon", session.ConfirmationGlyph);

        // Scan transition
        session.MarkScanned("192.168.1.50", "Mozilla/5.0 (iPhone)");
        Assert.Equal(QrSessionStatus.Scanned, session.Status);

        // Approve transition
        var ticket = session.Approve("usr_target_123", "pk_mobile_test", "enc_payload_sample");
        Assert.Equal(QrSessionStatus.Approved, session.Status);
        Assert.Equal("usr_target_123", session.AuthorizedUserId);
        Assert.Equal(ticket, session.ExchangeTicket);

        // Consume transition
        session.Consume();
        Assert.Equal(QrSessionStatus.Consumed, session.Status);
    }

    [Fact]
    public void IsExpired_ReturnsTrueWhenCurrentUtcExceedsExpiry()
    {
        var session = QrLoginSession.CreateNew(
            challengeNonce: "nonce-1",
            browserSecretHash: "hash-1",
            confirmationCode: "42",
            confirmationGlyph: "Sapphire Tiger",
            ttlSeconds: -1 // Expired immediately
        );

        Assert.True(session.IsExpired);
    }

    [Fact]
    public void Approve_WhenExpired_ThrowsInvalidOperationException()
    {
        var session = QrLoginSession.CreateNew(
            challengeNonce: "nonce-expired",
            browserSecretHash: "hash-expired",
            confirmationCode: "99",
            confirmationGlyph: "Ruby Dragon",
            ttlSeconds: -10
        );

        var ex = Assert.Throws<InvalidOperationException>(() => session.Approve("usr_1", null, null));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

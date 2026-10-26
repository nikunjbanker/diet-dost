<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261026-139: Mobile Web QR Approval View & Camera Scanner UX

- **Status**: Accepted
- **Date**: 2026-10-26
- **Domain**: Presentation / UI / UX
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: [#113](https://github.com/nikunjbanker/diet-dost/issues/113)

---

## 1. Context & Problem Statement
When scanning the QR login barcode from an Android or iOS device, mobile users need a dedicated, lightweight mobile approval view (`qr-approve.html`) that:
1. Renders cleanly on any mobile viewport (320px to 480px width) adhering to the Linear Obsidian-dark design system.
2. Displays the visual Match Code HUD (e.g. `[ 16 ] Jade Phoenix`) with explicit safety warnings to prevent QRLJacking or shoulder-surfing attacks.
3. Provides one-tap approval for signed-in mobile users, authentication fallback for unauthenticated mobile users, and quick one-tap demo approval for reviewers.
4. Provides real-time countdown timer tracking the 90-second ephemeral session TTL.

---

## 2. Decision & Implementation
1. **Dedicated Mobile Approval Page (`qr-approve.html`)**:
   - Built standalone lightweight HTML5 page without loading full desktop dashboard assets.
   - Embeds Match Code HUD displaying 2-digit confirmation number and glyph pair.
   - Includes safety verification callout alerting users to check the computer display before approving.
2. **Dedicated Mobile Approval Controller (`qr-approve-controller.js`)**:
   - Parses query parameters (`sid`, `nonce`, `code`, `glyph`).
   - Automatically manages session countdown timer.
   - Dispatches approval requests to `/api/auth/qr/approve` or `/api/auth/qr/simulate-approval`.
   - Transitions UI seamlessly into success state upon desktop ticket generation.
3. **Cache Invalidation & Version Bump**:
   - Updated asset cache busting queries (`?v=1.9.1`) across `main.js`, `api.js`, `auth-gate.js`, and `qr-auth-controller.js` to ensure mobile devices load fresh un-cached ES modules immediately.

---

## 3. Consequences & Forward Roadmap Impact
- **Positive**: Provides world-class cross-device authentication ergonomics identical to modern enterprise apps.
- **Positive**: Completely eliminates camera scanner dead-ends by opening directly to the approval HUD.
- **Positive**: Mobile BFF can reuse identical approval API endpoints for native mobile apps in Milestone 2.0.

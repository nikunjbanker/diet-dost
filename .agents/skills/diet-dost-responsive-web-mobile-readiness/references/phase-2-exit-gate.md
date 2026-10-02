<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Phase 2 Exit Gate: Transition from Shared Readiness to Native Mobile

> **Exit Gate Definition**: Formal criteria required to complete **Stage 2A (Responsive / Shared Preparation)** and greenlight **Stage 2B (Native Mobile Implementation)**.  

---

## 1. Stage 2A Exit Checklist

Before any native mobile project (.NET MAUI / Android / iOS) code is authored or merged, the following gates must be formally satisfied and verified:

### Gate 1: Responsive Web Presentation (Implement Now)
- [ ] Responsive CSS Grid and Flexbox layouts render flawlessly across mobile (`375px`, `390px`), tablet (`768px`, `820px`), and desktop (`1440px`, `1920px`).
- [ ] Bottom navigation bar is active and functional on viewports `<640px` and hidden on larger screens.
- [ ] Bottom sheet modals replace centered dialogs on mobile viewports for meal logging and quota alerts.
- [ ] All interactive elements strictly satisfy the `>= 44px x 44px` touch target requirement.
- [ ] Zero horizontal overflow (`scrollWidth === innerWidth`) verified via automated browser tests.

### Gate 2: Mobile BFF Contracts & Compression (Prepare Contract Now)
- [ ] `GET /api/mobile/v1/dashboard/composite` implemented in `MobileBffController` reusing Phase 1 shared CQRS query handlers.
- [ ] Compact payload optimization: strict camelCase JSON, null field omission, and ISO 8601 UTC timestamps.
- [ ] HTTP response compression (Brotli / Gzip) active on all mobile endpoints.
- [ ] HTTP `ETag` and `If-None-Match` caching verified with `304 Not Modified` return behavior.
- [ ] Automated integration test suite validates Mobile BFF composite endpoint across all 5 demo user tiers.

### Gate 3: Native Feature Classification & Documentation (Document for Native)
- [ ] Native vs. Web capability taxonomy approved and documented in `references/native-feature-classification.md`.
- [ ] Hardware-specific boundaries (SkiaSharp compression, AndroidKeyStore/Keychain, SQLite offline sync) clearly defined.
- [ ] Cross-platform parity verification matrix published in `references/cross-platform-parity-template.md`.

---

## 2. Stage 2B Handoff Signoff

When all Gate 1, Gate 2, and Gate 3 items above are checked:
1. Stage 2A is declared **COMPLETE**.
2. Stage 2B (Native Mobile Implementation) is formally **AUTHORIZED**.
3. Development proceeds to native .NET MAUI shell setup, hardware keystore integration, native camera capture, and offline SQLite synchronization.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Cross-Platform Functional & Visual Parity Verification Matrix

> **Scope**: Functional, Mathematical, Authorization, and Visual Parity across Web PWA, Android, and iOS  
> **Mandate**: Zero-Assumption Rule & Zero-Mathematical Drift across platforms  

---

## 1. Multi-Platform Parity Verification Matrix

| Acceptance Domain | Web PWA (Responsive) | Android (.NET MAUI) | iOS (.NET MAUI) | Parity Criterion & Invariant Rule | Pass / Fail |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Auth & Session** | Cookie + Bearer JWT | `AndroidKeyStore` + Bearer | iOS Keychain + Bearer | RFC 7519 JWT validation; identical token expiry (60m) & auto-refresh. | [ ] |
| **Clinical Math (ICMR-NIN 2024)** | Backend CQRS Handlers | Backend CQRS Handlers | Backend CQRS Handlers | **100% Exact Match**: Calorie, BMR, TDEE, and 6-macro calculations match to `0.01g` / `0.1 kcal`. | [ ] |
| **Tier Gating (Free / Basic / Prem)** | Role / Tier middleware | Role / Tier middleware | Role / Tier middleware | Exact parity on 7D/30D/90D history limits, scan quotas, and paywall locks. | [ ] |
| **Composite Hydration** | `/api/web/v1/dashboard/composite` | `/api/mobile/v1/dashboard/composite` | `/api/mobile/v1/dashboard/composite` | 1 single roundtrip hydrates HUD, macro dials, today's meals, and quota badge. | [ ] |
| **Photo Food Vision** | Multimodal AI Vision | Multimodal AI Vision + SkiaSharp | Multimodal AI Vision + SkiaSharp | Gemini 3 Flash Thinking analysis output, item breakdowns, and confidence scores match. | [ ] |
| **Touch Ergonomics** | Mobile bottom nav & bottom sheets | Native Shell tabs & bottom sheets | Native Shell tabs & bottom sheets | Thumb-zone reachability on all viewports `<= 430px` width. | [ ] |
| **Obsidian Dark Aesthetic** | Pure CSS tokens (`#0b0f17`, `#111827`) | MAUI XAML styles (`#0b0f17`, `#111827`) | MAUI XAML styles (`#0b0f17`, `#111827`) | Symmetrical color palette, neon accents, card elevations, and border radiuses. | [ ] |

---

## 2. Invariant Clinical Dietetics Verification

Under the Zero-Assumption Rule, no client platform is permitted to execute client-side calorie or macro estimations using hardcoded client tables or arbitrary formulas:

$$\text{Client Math Invariant: } \left| \text{Calories}_{\text{Web}} - \text{Calories}_{\text{Mobile}} \right| = 0.00$$

Every platform must dispatch requests to the centralized backend domain handlers (`POST /api/meals/estimate` or `/api/mobile/v1/meals/capture`), ensuring absolute uniformity across all devices.

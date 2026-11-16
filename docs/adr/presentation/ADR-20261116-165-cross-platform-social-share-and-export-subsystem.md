<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261116-165: Cross-Platform Social Share and Export Subsystem

- **Status**: Accepted
- **Date**: 2026-11-16
- **Domain**: Presentation / Web / Social / Security
- **Deciders**: Diet-Dost Product Owner & Architecture Team
- **Tracking Issue**: Social sharing for meal log detail and daily view across mobile, tablet, and desktop

## Context & Problem Statement

Sharing meal logs and daily fitness progress is the primary driver of peer accountability and organic user growth in diet and nutrition applications. Previously, Diet Dost had no export or sharing mechanisms. Users had to rely on manual, unformatted screen captures. Furthermore, social sharing requirements differ across device tiers:
- **Mobile (<640px)** users expect the native OS share sheet (WhatsApp, Instagram Stories, Facebook, AirDrop, Camera Roll) via Web Share API Level 2.
- **Desktop (>=1024px)** users work in browser tabs where OS share dialogs lack WhatsApp Web/Instagram Web integration. They need a 1-click clipboard image copy (`Ctrl+V`), high-DPI visual preview, and direct web app intents.
- **Privacy & DPDPA 2023**: Indian health privacy laws prohibit exposing personal identifiers (user IDs, emails, exact weights, or medications) in public shares.

## Decision

1. **Client-Side High-DPI Canvas Rendering Engine (`ShareCardRenderer.js`)**:
   - Pure client-side HTML5 offscreen canvas rendering (1080x1080 Square and 1080x1920 Story modes) with zero backend CPU overhead and zero external cloud rendering dependencies.
   - Generates context-specific cards for **Meal Log Entry Details** (dish title, meal badge, high-res plate photo with rounded clip, 5-macro distribution, portion pill tags, ICMR-NIN 2024 Dost insight, web app link) and **Daily Nutrition Pulse** (consumed vs budget gauge, deficit remaining, 4 macro meters, Daily Health Score 88/100, consistency streak, earned Indian badges, companion reflection, web app link).
2. **Device-Adaptive Dispatcher (`ShareService.js`)**:
   - On mobile with Web Share API Level 2 (`navigator.canShare({ files })`), dispatches the generated PNG file directly to the native OS share sheet.
   - On desktop and non-file-sharing mobile fallback, opens the Obsidian-dark **Share & Export Studio** modal (`share-modal.html` and `share-modal.js`).
3. **Desktop 1-Click Clipboard Image Copy (Hero Feature)**:
   - Uses `navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })])` so users can paste (`Ctrl+V`) high-resolution cards directly into WhatsApp Web, Slack, Discord, or Facebook.
   - Provides direct web app links for WhatsApp Web (`web.whatsapp.com`), Facebook, X (Twitter), Email (`mailto:`), and high-res PNG download.
4. **Touchpoint Integration**:
   - Integrated into Meal Review/Detail Modal (`review-modal.html`), Meal History timeline cards and data grid (`MealDiaryView.js`), and Daily Calorie Balance Hero HUD (`hero-hud.html`).
5. **DPDPA 2023 Compliance**:
   - Strictly omits all PII (user IDs, emails, phone numbers, medications) from card rendering and formatted text.

## Consequences

- Users on mobile and desktop can share meals and daily achievements seamlessly in under 50ms.
- High-resolution cards prominently display the web application URL (`https://dietdost.app`) for viral growth.
- Zero server compute or 3rd-party rendering costs.
- Fully compliant with modal stack LIFO layering and WCAG 2.2 touch target minimums (>= 44x44px).

## Verification

- `dotnet test tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj --filter FullyQualifiedName~SocialShareSubsystemContractTests` — 9 passed.
- `npx eslint src/Nutrition.WebGateway/wwwroot/js/**/*.js` — 0 errors, 0 warnings.
- `dotnet test tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj` — 404 passed, 0 failed.

## Forward Roadmap Impact & Future Phase Compatibility

The subsystem is fully modular, event-bus decoupled, and contained within the Web Gateway presentation layer. When Phase 2B (.NET MAUI native app) launches, the same `ShareCardRenderer` logic can be ported to SkiaSharp, while the Web PWA continues leveraging the standards-based Web Share API Level 2.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261117-166: Futuristic Share Button Icon, Exact Dev Link Attribution, and Shared Link Deduplication

- **Status**: Accepted
- **Date**: 2026-11-17
- **Domain**: Presentation / UI / Social / Web
- **Deciders**: Diet-Dost Product Owner & Architecture Team
- **Tracking Issue**: [#159](https://github.com/nikunjbanker/diet-dost/issues/159)

## Context & Problem Statement

Following initial user testing of the Cross-Platform Social Share subsystem (ADR-165), three critical presentation and user experience defects were reported:
1. **Unattractive / Inconsistent Share Icon**: The legacy outbox tray emoji (`📤`) rendered inconsistently across mobile and desktop platforms, feeling clunky and breaking Diet Dost's high-tech, Obsidian-dark design system.
2. **Incorrect Domain Attribution**: The application link was displaying `dietdost.app` rather than the official active development environment `https://dev.diet-dost.in`.
3. **Duplicate Link Display at Bottom**: When sharing via native mobile OS share sheets or WhatsApp, the application link appeared twice next to each other at the bottom of the message (e.g. `... https://dev.diet-dost.in https://dev.diet-dost.in`). This occurred because both `text` (already containing the link) and `url` were passed into `navigator.share()`, triggering automatic concatenation by mobile operating systems and messaging apps.

## Decision

1. **Futuristic & Innovative SVG Share Iconography (`.icon-share-futuristic`)**:
   - Replaced all instances of `📤` with a standard, futuristic, and innovative vector SVG icon featuring 3 connected quantum telemetry nodes (top, hub, bottom) and animated link beams (`shareBeamFlow`).
   - Integrated into:
     - Daily Calorie Balance Hero HUD (`#btn-share-daily-hud`)
     - Meal Review Modal (`#btn-share-review-meal`)
     - Meal History Diary Cards & Grid (`.btn-share-meal`)
   - Applied cybernetic Obsidian glow micro-interactions on hover with dual-tint emerald (`#27c380`) and indigo (`#818cf8`) radiance.
2. **Exact Development Link Attribution (`https://dev.diet-dost.in`)**:
   - Standardized application attribution URL across `ShareCardRenderer.js`, `share-service.js`, `share-modal.html`, and `share-modal.js` to strictly `https://dev.diet-dost.in`.
   - Updated canvas card rendering footer with dynamic label width measurement to prevent text clipping or misalignment.
3. **Shared Link Deduplication**:
   - Omitted the redundant `url` parameter in `navigator.share()` across `share-service.js` and `share-modal.js` when `text` already contains the application URL.
   - Guaranteed that WhatsApp, Signal, Telegram, and mobile OS share sheets display the web application address exactly once at the bottom.

## Consequences

- Visual design conforms to the Obsidian Linear.app design standard with consistent high-tech iconography.
- Zero duplicate URL concatenation in external messaging apps and social channels.
- Authoritative development link `https://dev.diet-dost.in` is uniformly displayed across canvas cards, modals, and clipboard text.

## Verification

- `SocialShareSubsystemContractTests`: 11 contract tests passed (100% green).
- Full EvalHarness suite: 406 tests passed, 0 warnings.
- `npx eslint`: 0 errors, 0 warnings.
- DevSecOps `verify-ai-security-defense.ps1 -Mode All`: All 7 defense vectors passed.

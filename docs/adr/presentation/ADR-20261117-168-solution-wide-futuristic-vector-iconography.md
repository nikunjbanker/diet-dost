<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261117-168: Solution-Wide Futuristic Vector Iconography Modernization

- **Status**: Accepted
- **Date**: 2026-11-17
- **Domain**: Presentation / UI / Design System / Web
- **Deciders**: Diet-Dost Product Owner & Architecture Team
- **Tracking Issue**: [#163](https://github.com/nikunjbanker/diet-dost/issues/163)

## Context & Problem Statement

Following the initial implementation of the centralized SVG sprite architecture in ADR-167, a thorough end-to-end audit of all interactive screens, modal dialogs, status badges, and JavaScript UI controllers revealed lingering legacy system emojis and raw text glyphs across multiple core surfaces:
1. **User & Tier Management (`admin-modal.html` & `admin-modal.js`)**: Employed legacy emojis (`👥` Users, `🟢` Activity, `⚙️` Settings, `📡` Telemetry, `🗑️` Purged, `➕` Add User, `🔄` Sync, `✏️` Edit, `🔒` Deactivate, `🔓` Activate) for tabs, table action buttons, and status indicators.
2. **Application Header (`header.html` & `main.js`)**: Included legacy emojis (`👤` User Profile, `⚡` Upgrades, `🩺` Clinical, `📊` Telemetry, `🛡️` SuperAdmin, `🚪` Signout) and raw emoji tier pills (`🆓 Free`, `🥉 Basic`, `💎 Premium`, `👑 SuperAdmin`).
3. **Meal Logger Workspace (`meal-logger.html` & `meal-logger.js`)**: Used legacy camera/microphone and food category emojis (`📸`, `🎙️`, `⚡`, `🔍`, `🌾`, `🥑`, `🥗`, `🍬`, `🧂`).
4. **Auth Gate & Mobile Bridge (`auth-gate.html` & `auth-gate.js`)**: Used emojis (`💻` Desktop notice, `⚡` QR, `🔑` Recovery, `📱` Phone, `🔒` Disabled) across auth modes and demo tier selection buttons.
5. **Progress & Biometrics HUD (`progress-modal.html` & `progress-modal.js`)**: Used raw emojis (`👤`, `📐`, `🖼️`, `📸`, `🔒`, `⚡`) on tabs, dropzones, and locked tier overlays.
6. **Review & Plate Lightbox (`review-modal.html`)**: Used emojis (`🤖`, `🔍`, `🔥`, `💪`, `🌾`, `🥑`, `🥗`, `🍬`, `🩺`, `🎯`, `👍`, `👎`, `⚡`) for AI model badges, macro chips, dietitian advice, and model feedback buttons.
7. **Delete Meal Confirmation & Diary (`delete-meal-modal.html` & `MealDiaryView.js`)**: Used emojis (`ℹ️`, macro chips, verified checkmarks) in deletion summaries and diary cards.
8. **Companion Card & Transparency Modal (`companion-card.html`, `transparency-modal.html`, `index.html`)**: Used emojis (`🩺`, `🔥` streak, `🖨️` print, `✓` sync).

These emojis caused visual fragmentation across operating systems and broke the Obsidian dark, high-precision Linear.app design aesthetic. A solution-wide eradication was required to modernize all 8 surfaces under the centralized SVG sprite sheet architecture.

## Decision

1. **Exhaustive Eradication of Legacy System Emojis**:
   - Replaced all legacy system emojis across all 8 surfaces with high-precision vector SVG symbols defined in `src/Nutrition.WebGateway/wwwroot/assets/icons/sprite.svg`.
   - Expanded the sprite sheet from 21 icons to 65 standardized symbols, with 44 new standalone SVG asset files created in `src/Nutrition.WebGateway/wwwroot/assets/icons/`.

2. **Categorized Vector Iconography Expansion (65 Total Symbols)**:
   - **Navigation & Telemetry**: `#icon-users`, `#icon-activity`, `#icon-settings`, `#icon-telemetry`, `#icon-bell`, `#icon-shield-check`, `#icon-signout`, `#icon-desktop`, `#icon-phone`, `#icon-printer`.
   - **User Tiers & Security**: `#icon-tier-free`, `#icon-tier-basic`, `#icon-tier-premium`, `#icon-tier-superadmin`, `#icon-user`, `#icon-lock`, `#icon-unlock`, `#icon-bolt`, `#icon-key`.
   - **Inputs & Capture Controls**: `#icon-camera`, `#icon-mic`, `#icon-image`, `#icon-scan`, `#icon-search`, `#icon-clock`, `#icon-folder`, `#icon-plus`, `#icon-refresh`.
   - **Macronutrients & Clinical**: `#icon-fire` (calories), `#icon-muscle` (protein), `#icon-grain` (carbs), `#icon-lipid` (fats), `#icon-leaf` (fiber), `#icon-cube` (sugar), `#icon-shaker` (sodium), `#icon-stethoscope` (companion/dietitian).
   - **Biometrics & Feedback**: `#icon-face`, `#icon-body`, `#icon-target`, `#icon-thumb-up`, `#icon-thumb-down`, `#icon-bot`, `#icon-info`, `#icon-check`.
   - **Social & Share Studio**: `#icon-share-futuristic`, `#icon-x-twitter`, `#icon-whatsapp`, `#icon-facebook`, `#icon-clipboard-copy`, `#icon-download`, `#icon-copy-text`, `#icon-email`, `#icon-system-share`, `#icon-view-cards`, `#icon-view-grid`, `#icon-export-excel`, `#icon-close`.
   - **Astronomical Meals**: `#icon-meal-breakfast`, `#icon-meal-lunch`, `#icon-meal-snack`, `#icon-meal-dinner`.

3. **Modernization of 8 Key Surfaces**:
   - `admin-modal.html` and `admin-modal.js`: Modernized tabs, table action buttons, and live connection status indicators with `.icon-status-online` and `.icon-status-recent` pulse animations.
   - `header.html` and `main.js`: Upgraded profile menu triggers, telemetry launchers, sign-out controls, and dynamic tier pills to use vectorized icons.
   - `meal-logger.html` and `meal-logger.js`: Modernized capture modes, sample thali quick loader, scanning optics, and food suggestion chips.
   - `auth-gate.html` and `auth-gate.js`: Replaced desktop banners, auth mode tabs, demo tier pills, and recovery dropzone icons.
   - `progress-modal.html` and `progress-modal.js`: Replaced biometric tabs, camera upload dropzones, and locked tier overlays.
   - `review-modal.html`: Modernized AI model badges, confidence checks, 6-macro chips, clinical advice badges, and model feedback buttons.
   - `delete-meal-modal.html` and `MealDiaryView.js`: Modernized preview macro mini-pills, advisory notices, diary card chips, and verified badges.
   - `companion-card.html`, `transparency-modal.html`, and `index.html`: Upgraded companion avatar, streak flame, print report button, and live sync badges.

4. **Zero-Duplication Asset & CSS Architecture**:
   - All consumer views reference icons using `<svg class="icon-svg"><use href="assets/icons/sprite.svg#icon-<id>"></use></svg>` (or `#icon-<id>`).
   - Implemented reusable CSS utility classes (`.icon-svg`, `.icon-tab`, `.icon-btn`, `.icon-heading`, `.icon-status-online`, `@keyframes statusPulse`).
   - Configured `currentColor` inheritance so icons inherit text/theme color and glow effects dynamically.

5. **Contract Test Governance**:
   - Expanded `FuturisticIconographyAssetContractTests.cs` to assert existence of all 65 standalone SVGs and sprite symbols.
   - Added automated contract assertions validating the complete absence of legacy emojis in all 8 modernized HTML and JavaScript files.

## Consequences

- Consistent cybernetic, Obsidian-dark aesthetic adhering to Linear.app standards across the entire application.
- Complete elimination of OS-dependent emoji rendering differences across Windows, macOS, iOS, and Android.
- Zero markup duplication with cached, unified symbol sprite sheet resolution.
- Contract test coverage guarantees zero emoji regression in future features.

## Verification

- `FuturisticIconographyAssetContractTests`: All 11 contract tests pass with 0 errors.
- Full `dotnet test` suite: All test projects pass with 0 errors and 0 warnings.
- `npx eslint`: 0 errors across all JavaScript controllers.
- Multi-tier CFT verification (`tests/validate_e2e_tiers.ps1`): 100% pass across all 5 user tiers.


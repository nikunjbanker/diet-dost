<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261117-167: Centralized SVG Icon Asset and Sprite Architecture for Futuristic Iconography

- **Status**: Accepted
- **Date**: 2026-11-17
- **Domain**: Presentation / UI / Design System / Web
- **Deciders**: Diet-Dost Product Owner & Architecture Team
- **Tracking Issue**: [#161](https://github.com/nikunjbanker/diet-dost/issues/161)

## Context & Problem Statement

Following the successful rollout of the futuristic `.icon-share-futuristic` share button (ADR-166), an audit of all icons and glyphs across the Diet-Dost web application identified several visual and architectural deficiencies:
1. **Legacy Glyphs & System Emojis**: System emojis and raw Unicode symbols were used for interactive controls (e.g. `✏️` Edit, `🗑️` Delete, `✕` Close, `🎴` Cards, `📊` Grid, `📥` Export, `🐦` obsolete Twitter bird, and meal timing emojis `🌅`, `☀️`, `☕`, `🌙`). These rendered inconsistently across operating systems (Windows, macOS, iOS, Android), had clunky aesthetic characteristics, and broke the Obsidian dark, high-tech Linear.app design aesthetic.
2. **Inline SVG Duplication Risk**: Embedding raw multi-line SVG path definitions inline across multiple HTML partials and JavaScript view renderers duplicated DOM markup, bloated bundle sizes, and made global style changes difficult to maintain.
3. **Lack of Agent & Skill Memory Governance**: Without persistent guidelines in agent rules, future agent turns and developer PRs risked re-introducing raw emojis or redundant inline SVGs for new UI features.

## Decision

1. **Centralized Asset & Sprite Sheet Architecture**:
   - Created `src/Nutrition.WebGateway/wwwroot/assets/icons/` containing 21 standalone vector SVG files for isolated asset delivery.
   - Built a unified SVG symbol sprite sheet `src/Nutrition.WebGateway/wwwroot/assets/icons/sprite.svg` containing all 21 icons as `<symbol id="icon-<name>">` elements.
   - Preloaded and registered `sprite.svg` in `index.html` via `<div data-include="assets/icons/sprite.svg"></div>` for zero-latency local symbol hydration.
2. **Standard Consumer Reference Protocol**:
   - All consumer templates and components reference icons using `<svg class="..."><use href="assets/icons/sprite.svg#icon-<name>"></use></svg>` (or `#icon-<name>`).
   - Standardized 21 canonical symbols across actions (`icon-share`, `icon-edit`, `icon-delete`, `icon-close`), views (`icon-view-cards`, `icon-view-grid`, `icon-export-excel`, `icon-aspect-square`, `icon-aspect-story`), social/clipboard (`icon-clipboard`, `icon-x-twitter`, `icon-whatsapp`, `icon-download`, `icon-copy-text`, `icon-facebook`, `icon-email`, `icon-system-share`), and astronomical meal times (`icon-meal-breakfast`, `icon-meal-lunch`, `icon-meal-snack`, `icon-meal-dinner`).
3. **Zero-Emoji UI Control Standard & Sleek Aesthetic**:
   - Replaced all legacy emojis with futuristic, crisp vector SVGs using `currentColor` stroke or fill to inherit theme tokens and hover glow effects (e.g. cyan `#38bdf8` edit glow, crimson `#f87171` delete glow, emerald `#27c380` excel glow).
   - Replaced raw `✕` glyphs across all 10 modal dialogs with precision `.icon-modal-close` crosshairs featuring smooth 90° rotation on hover.
   - Replaced Twitter bird with official minimalist geometric vector `X` logo (`#icon-x-twitter`) and updated label to `X (Twitter)`.
4. **Agent & Skill Memory Codification**:
   - Codified Rule 7 in `.agents/rules/ui-ux-design-system.md` mandating centralized SVG assets and zero-duplication standard.
   - Updated `.agents/skills/design-system-enforcer/SKILL.md` with procedural guidelines and verification checklists.
   - Updated `DESIGN.md` with authoritative Vector Iconography Asset & Sprite Sheet Architecture specification.

## Consequences

- Complete visual consistency adhering to Linear / Obsidian dark aesthetic with zero raw emojis on UI controls.
- Zero duplicate SVG path markup across HTML partials and JavaScript loops.
- Instant, cached symbol rendering with dynamic CSS tinting and hover micro-interactions.
- Permanent agent memory prevents future regression or icon sprawl.

## Verification

- `FuturisticIconographyAssetContractTests`: 9 contract tests passed (100% green).
- Full EvalHarness suite: 415 tests passed, 0 warnings.
- `npx eslint`: 0 errors, 0 warnings across all frontend JavaScript.


<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Authoritative UI/UX Design System Mandate (`DESIGN.md`)

This rule mandates that `DESIGN.md` in the solution root is the **single authoritative source of truth** for all user interface, user experience, typography, surface elevation, and design-token decisions in Diet-Dost.

---

## Mandatory UI/UX Engineering Principles

1. **Authoritative Source of Truth**:
   - `DESIGN.md` defines the canonical design tokens: colors (`colors.canvas` #010102, `colors.surface-1` through `colors.surface-4`, `colors.hairline`, `colors.primary` #5e6ad2), typography scale, border-radius tokens (`rounded.xs` to `rounded.pill`), spacing ladder (4px base unit), and component definitions.
   - Always read and inspect `DESIGN.md` before initiating ANY UI/UX implementation or modification.

2. **Zero-Ad-Hoc Design Rule**:
   - Never introduce arbitrary or ad-hoc visual conventions, unapproved saturated colors (bright greens, reds, blues), atmospheric background gradients, or non-standard corner radiuses when `DESIGN.md` already defines the token or rule.
   - The single chromatic accent is **Linear lavender-blue** (`#5e6ad2`). The only approved semantic colors are status green (`#27a644`), alert red (`#eb5757`), and amber (`#f5a623`).

3. **Surface Elevation & Hairline Borders**:
   - Depth is carried strictly by the 4-step surface ladder (Canvas `#010102` → Surface 1 `#0f1011` → Surface 2 `#141516` → Surface 3 `#18191a` → Surface 4 `#191a1b`) and 1px hairline borders (`#23252a`).
   - Resist drop shadows on dark canvases.

4. **Component Consistency & Reusability**:
   - Buttons: `button-primary` (lavender), `button-secondary` (charcoal surface-1), `button-tertiary` (flat canvas). Corner radius: `rounded.md` (8px). Never pill-round primary or secondary action buttons.
   - Cards & Modals: Surface 1 or 2 panels, hairline border, `rounded.lg` (12px) or `rounded.xl` (16px) corners.
   - Form Inputs: Surface 1 background, hairline border, 2px focus ring (`colors.primary-focus` #5e69d1 outline at 50% opacity).

5. **Responsive Behavior & Touch Governance**:
   - Mobile touch targets must maintain $\ge 44 \times 44\text{px}$ on touch viewports ($\le 639\text{px}$).
   - Responsive breakpoints: Mobile ($\le 639\text{px}$), Tablet ($640\text{px} - 1023\text{px}$), Desktop ($\ge 1024\text{px}$).
   - Zero horizontal overflow (`scrollWidth <= innerWidth`) across all device viewports.

6. **Pre-PR Verification Checklist for UI Tasks**:
   - [ ] Verified compliance with `DESIGN.md` tokens.
   - [ ] Maintained visual component consistency.
   - [ ] Tested responsive behavior on Desktop (1440px), Tablet (768px), and Mobile (375px/390px).
   - [ ] Verified accessibility (contrast, ARIA roles, keyboard focus indicators).
   - [ ] Executed visual verification via `browser_subagent` or headless CDP inspection.

7. **Mandatory Centralized SVG Icon Asset & Zero-Duplication Standard**:
   - **Zero-Duplication Asset Rule**: Never embed raw multi-line SVG strings inline inside HTML partials, components, or JavaScript renderers.
   - **Asset Storage**: Store all icons as standalone vector SVG files in `src/Nutrition.WebGateway/wwwroot/assets/icons/<name>.svg` and compile them into the unified symbol sprite sheet `src/Nutrition.WebGateway/wwwroot/assets/icons/sprite.svg`.
   - **Consumer Reference**: Consumers (HTML partials, JS renderers) must reference icons using `<svg class="..."><use href="assets/icons/sprite.svg#icon-<name>"></use></svg>` (or `#icon-<name>` when the sprite is loaded into the DOM).
   - **Zero-Emoji UI Control Rule**: Never use system emojis (`✏️`, `🗑️`, `🌅`, `☀️`, `☕`, `🌙`, `🐦`, `✕`, etc.) for UI controls, affordances, modal close buttons, social intents, or meal category badges. All UI controls and categorization badges must use clean, futuristic vector SVGs following the Linear / Obsidian dark aesthetic with `currentColor` stroke or fill and glowing hover micro-interactions.


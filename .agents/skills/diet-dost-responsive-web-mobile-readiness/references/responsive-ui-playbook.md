<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Responsive UI Architecture & Implementation Playbook

> **Execution Stage**: Stage 2A (Responsive / Shared Preparation — Implement Now)  
> **Target Scope**: Native CSS Grid, Flexbox, Fluid Typography, Touch Targets, Obsidian Dark  

---

## 1. Breakpoint System & Media Queries

Diet-Dost uses a mobile-first, 3-tier breakpoint system aligned with modern device categories:

```css
/* ==========================================================================
   Diet-Dost Standard Breakpoint Tokens
   ========================================================================== */
:root {
  --breakpoint-mobile-max: 639px;
  --breakpoint-tablet-min: 640px;
  --breakpoint-tablet-max: 1023px;
  --breakpoint-desktop-min: 1024px;
  --breakpoint-wide-min: 1440px;
}

/* Base Styles: Mobile (< 640px) by default */

/* Tablet Portrait & Landscape (640px - 1023px) */
@media (min-width: 640px) {
  /* Tablet adjustments: 2-column grids, expanded header */
}

/* Desktop & Laptop (>= 1024px) */
@media (min-width: 1024px) {
  /* Desktop layout: 3-column dashboard, sticky navigation rail */
}

/* Ultra-wide / 4K Monitors (>= 1440px) */
@media (min-width: 1440px) {
  /* Max container clamping to prevent excessive line length */
  .app-container {
    max-width: 1400px;
    margin-inline: auto;
  }
}
```

---

## 2. Touch Target Governance (WCAG 2.2 / Apple HIG)

Every interactive element (button, icon, tab, picker) must satisfy the minimum touch target standard:

- **Minimum Size**: `44px x 44px` (or `48px x 48px` where space permits).
- **Minimum Tap Spacing**: At least `8px` separation between adjacent interactive targets.
- **CSS Utility**:
```css
.touch-target {
  min-width: 44px;
  min-height: 44px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  touch-action: manipulation; /* Disables double-tap zoom delay */
}
```

---

## 3. Responsive Touch HUD & Macro Gauges

The Caloric HUD and 6-Macro progress rings must automatically adapt between mobile card stacks and desktop horizontal strips:

```css
/* Mobile: Compact 2x2 or 2x3 Grid for Macro Dials */
.macro-hud-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
}

/* Tablet: 3 columns */
@media (min-width: 640px) {
  .macro-hud-grid {
    grid-template-columns: repeat(3, 1fr);
    gap: 1rem;
  }
}

/* Desktop: 6 columns inline */
@media (min-width: 1024px) {
  .macro-hud-grid {
    grid-template-columns: repeat(6, 1fr);
    gap: 1.25rem;
  }
}
```

---

## 4. Mobile Browser Safe Areas & Fluid Spacing

Ensure content never gets obscured by phone notches, dynamic islands, or home gesture bars:

```css
body {
  padding-top: env(safe-area-inset-top, 0px);
  padding-bottom: env(safe-area-inset-bottom, 0px);
  padding-left: env(safe-area-inset-left, 0px);
  padding-right: env(safe-area-inset-right, 0px);
  overflow-x: hidden; /* Guarantee zero horizontal scroll on mobile */
}

/* Fluid Typography using clamp() */
h1 {
  font-size: clamp(1.5rem, 4vw + 0.5rem, 2.5rem);
}
h2 {
  font-size: clamp(1.25rem, 3vw + 0.25rem, 2rem);
}
```

---

## 5. Implementation Verification Checklist

- [ ] Viewport meta tag configured: `<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">`.
- [ ] Zero horizontal overflow (`window.innerWidth === document.documentElement.clientWidth`).
- [ ] All clickable icons and buttons pass `44x44px` bounding client rect assertion.
- [ ] High contrast ratios preserved across Obsidian-dark surfaces (text `#ffffff` / `#94a3b8` on `#0b0f17` background).
- [ ] CSS Grid and Flexbox layouts render cleanly on Chrome, Safari Mobile, and Firefox Mobile.

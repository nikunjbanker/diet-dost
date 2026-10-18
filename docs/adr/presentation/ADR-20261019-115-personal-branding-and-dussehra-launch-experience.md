<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261019-115: Permanent Personal Branding, Alpha Version Notice, and Dussehra Festival Launch Carousel

> **Date / Timestamp**: 2026-10-19T00:55:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: UI/UX & Clinical Dietetics Domain Lead (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[BRAND_IDENTITY]`, `[MARKETING_LAUNCH]`, `[UX_ERGONOMICS]`  
> **Affected Subsystems**: WebGateway | Presentation (`wwwroot/partials/`, `wwwroot/index.html`, `wwwroot/terms.html`, `wwwroot/clinical-health-consent.html`, `wwwroot/styles.css`, `wwwroot/js/main.js`)  
> **Associated PR & Stack**: Stack #3 (Branch: `feature/personal-branding-and-dussehra-launch`, Base: `feature/brand-icon-and-loading-screen`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Change Rationale

* Establishes permanent, elegant personal branding across all application pages honoring creator and visionary **Nikunj Banker**:
  - Authoritative phrase: **"Created with ❤️ by Nikunj Banker — with health and happy diet food"**.
  - Dual-Layer visibility architecture:
    1. **Full Obsidian Global Footer** ([`src/Nutrition.WebGateway/wwwroot/partials/footer.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/footer.html)) at the base of every page ([`index.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/index.html), [`terms.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/terms.html), [`clinical-health-consent.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/clinical-health-consent.html)).
    2. **Always-Visible Floating Maker Chip** (`.floating-maker-chip`) docked persistently at the viewport bottom-right on desktop (`bottom: 24px; right: 24px; z-index: 990;`) and elevated above the mobile thumb navigation on screens `<640px` (`bottom: calc(64px + env(...) + 12px); right: 12px;`).
* Integrates prominent **Alpha Version Warnings**:
  - Clear amber callout strip in the global footer: **`⚠️ Alpha Version Notice: Diet-Dost is currently in active Alpha preview (v1.5.0)...`**.
  - Compact `Alpha` pill inside the floating maker chip and banner header.
* Implements the **All-in-One Dussehra (Vijayadashami) Festival Launch Dynamic Carousel** ([`src/Nutrition.WebGateway/wwwroot/partials/dussehra-launch-banner.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/dussehra-launch-banner.html)):
  - Unites 4 culturally resonant, clinical-first launch concepts:
    1. *Concept 1 (Metabolic Victory)*: 🏹 Vanquish the 10 Metabolic Foes with AI Precision.
    2. *Concept 2 (Auspicious Beginning)*: ✨ Shubh Aarambh — Gift Yourself the Gold of True Health.
    3. *Concept 3 (Mindful Celebration)*: 🍯 Celebrate Festive Delicacies with Pure Joy & Mindful Balance.
    4. *Concept 4 (Founder's Dedication)*: 👑 Dussehra Launch Dedication — Created with ❤️ by Nikunj Banker.
  - Interactive features: 4-tab quick-switcher, indicator dots, auto-advance (7.5s), collapse/expand ticker with `localStorage` memory, and a 1-click *Shubh Sankalp* health pledge button that fires confetti and toast celebrations.

---

## 2. Context and Problem Statement

Prior to this decision:
1. **Absence of Shared Global Footer**: Pages ended abruptly after card containers. Static legal pages (`terms.html`, `clinical-health-consent.html`) had only bare link buttons.
2. **Missing Attribution**: There was no prominent recognition of the platform author, Nikunj Banker, or direct linking to the official GitHub repository (`https://github.com/nikunjbanker/diet-dost`).
3. **No Alpha State Notice**: Users were not explicitly informed that the platform is currently in an early active Alpha preview.
4. **No Festive Launch Engagement**: The milestone launch on the auspicious day of Dussehra lacked an interactive, celebratory gateway connecting traditional festivals with clinical portion intelligence.

---

## 3. Decision Drivers

* **Linear Obsidian Design System Compliance**: All new elements strictly adhere to [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) (Obsidian `#090a0d`, hairline borders `rgba(255, 255, 255, 0.08)`, gold marigold `#f59e0b`, and emerald `#10b981`).
* **Zero Collision Mobile Ergonomics**: The floating chip must NEVER overlap the fixed 64px `.mobile-bottom-nav` or its center meal logging floating action button.
* **Positive Framing**: Completely eliminates negative or restrictive words (e.g. "guilt"), replacing them with empowering phrases such as "Pure Joy & Mindful Balance".
* **Universal & Inclusive Scope**: Free of restrictive regional tags, creating a welcoming experience for all users.

---

## 4. Considered Options

* **Option A: Static Footer Only**: Add an Obsidian footer at the bottom of pages only. Rejected because users only see the attribution when scrolling to the very bottom.
* **Option B: Fixed Floating Banner Only**: Display a sticky banner across the bottom. Rejected because full-width bars consume excessive screen real estate and collide with mobile navigation.
* **Option C (Accepted): Dual-Layer Architecture (Full Obsidian Footer + Micro Floating Capsule)**: Full comprehensive details in the footer plus an always-visible, micro-sized glassmorphic badge docked safely in the corner.

---

## 5. Technical Implementation Details

```
   ┌────────────────────────────────────────────────────────────────────────┐
   │ PERSISTENT FLOATING MAKER CHIP (Always Visible)                        │
   │                                                                        │
   │   [ 🌿 Icon | Created with ❤️ by Nikunj Banker • Happy Diet Food | Alpha | ⭐ Repo ]
   │   • Desktop: bottom: 24px, right: 24px                                 │
   │   • Mobile: bottom: 76px, right: 12px (above 64px mobile-bottom-nav)    │
   └────────────────────────────────────────────────────────────────────────┘
```

1. **Shared Partial**: Created [`src/Nutrition.WebGateway/wwwroot/partials/footer.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/footer.html).
2. **Dussehra Carousel**: Created [`src/Nutrition.WebGateway/wwwroot/partials/dussehra-launch-banner.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/dussehra-launch-banner.html).
3. **Controller Logic**: Implemented `setupDussehraBanner()` in [`src/Nutrition.WebGateway/wwwroot/js/main.js`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/main.js).
4. **CSS Token Extensions**: Appended rules for `.app-global-footer`, `.floating-maker-chip`, `.footer-alpha-callout`, and `.dussehra-banner-card` to [`src/Nutrition.WebGateway/wwwroot/styles.css`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/styles.css).

---

## 6. Consequences & Living Synchronization

* **Positive**:
  - Consistent personal branding and open-source attribution visible across all viewports.
  - Transparent Alpha status notice prevents clinical misunderstanding.
  - Festive Dussehra launch carousel elevates excitement and user engagement.
* **Living Documentation Synchronized**:
  - Updated [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md) Section 4.
  - Automatically indexed in `docs/adr/index.json` and `docs/adr/README.md`.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Customer & Functional Acceptance Test (CFT): Responsive Web & Tablet Ergonomics

> **Test Suite**: Suite 4 — Multi-Viewport Responsive Ergonomics, Touch Navigation & Zero CLS  
> **Classification**: Mandatory Pre-PR & Release Acceptance Test Harness  
> **Target Subsystem**: Web Presentation Layer (`wwwroot/index.html`, `wwwroot/css/*`, `wwwroot/js/*`)  
> **Associated Milestone**: `Alpha Release 01 (Workable Web Showcase MVP on Azure)`  
> **Governing Skill**: [`diet-dost-responsive-web-mobile-readiness`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md)  
> **Reference Playbook**: [`references/responsive-cft-template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/references/responsive-cft-template.md)  

---

## 1. Overview & Test Objectives

This document establishes the official **Customer & Functional Acceptance Test (CFT)** suite for validating responsive web behavior across smartphone, tablet, and desktop viewport dimensions.

### Core Objectives:
1. **Zero Horizontal Layout Overflow**: Assert `document.documentElement.scrollWidth === window.innerWidth` across all supported viewports.
2. **Thumb-Zone Navigation on Mobile (<640px)**: Validate that the bottom navigation bar is fixed, responsive, and clearly displays active state.
3. **Bottom Sheet Modals on Mobile (<640px)**: Assert that meal logging dialogs and quota notices slide up as bottom sheets rather than centered popups.
4. **Touch Target Size Governance**: Assert that all interactive elements satisfy the `>= 44px x 44px` standard (WCAG 2.2 AA / Apple HIG).
5. **Cumulative Layout Shift (CLS)**: Assert CLS score `< 0.05` during composite hydration on cellular 4G throttling.

---

## 2. Supported Viewport Matrix

| Category | Target Profile | Resolution | Orientation | Navigation Expectation |
| :--- | :--- | :--- | :--- | :--- |
| **Mobile Compact** | iPhone SE / iPhone 13 mini | `375 x 667` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Mobile Standard** | iPhone 14/15/16, Pixel 7 | `390 x 844` / `412 x 915` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Tablet Portrait** | iPad Mini, iPad 10th Gen | `768 x 1024` / `820 x 1180` | Portrait | Adaptive Header + 2-Column Macro Grid |
| **Tablet Landscape** | iPad Air, iPad Pro 11" | `1180 x 820` / `1024 x 768` | Landscape | Split-View Layout (HUD/Charts left, Meals right) |
| **Desktop / Laptop** | MacBook Air, 1080p Monitor | `1440 x 900` / `1920 x 1080` | Landscape | 3-Column Obsidian Dashboard + Sticky Rail |

---

## 3. Execution Verification Checklist

### 3.1 Layout Stability & Viewport Boundaries
- [ ] Mobile 375px: Zero horizontal scroll bar; text wraps without clipping.
- [ ] Mobile 390px: Caloric summary HUD stacks vertically or as a 2x2 grid cleanly.
- [ ] Tablet 768px: Top header expands with profile and tier badge; bottom nav bar is hidden.
- [ ] Desktop 1440px+: 3-column dashboard layout renders with max-container clamping (`max-width: 1400px`).

### 3.2 Touch Targets & Interactivity
- [ ] All bottom nav bar items measure `>= 44px x 44px`.
- [ ] Quick Action buttons (e.g. `+ Log Meal`) provide haptic scale `:active` visual response.
- [ ] Date picker controls and navigation arrows satisfy minimum 8px spacing.

### 3.3 Multi-Tier Feature Gating on Mobile Viewports
- [ ] **Free Tier (`free@dietdost.app`)**:
  - History is restricted to 7 days; 30D/90D tabs show locked tier paywall.
  - Locked feature buttons (e.g. Export, Face Progress) are hidden or cleanly disabled.
- [ ] **Basic Tier (`basic@dietdost.app`)**:
  - History renders 30 days of data smoothly with mobile touch scrolling.
- [ ] **Premium Tier (`premium@dietdost.app`)**:
  - 90-day history and export features available; chart touch tooltips visible.

---

## 4. Automated Execution Script Snippet

```bash
# Automated headless browser viewport execution via Playwright / Subagent
node -e "
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const viewports = [
    { name: 'iPhone 14', width: 390, height: 844 },
    { name: 'iPad Portrait', width: 768, height: 1024 },
    { name: 'Desktop 1080p', width: 1920, height: 1080 }
  ];
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.goto('http://localhost:5240/');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    console.log(\`\${vp.name}: \${overflow ? 'FAILED (Overflow)' : 'PASSED'}\`);
  }
  await browser.close();
})();
"
```

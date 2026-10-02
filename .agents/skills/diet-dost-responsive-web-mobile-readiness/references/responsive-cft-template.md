<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Customer & Functional Acceptance Test (CFT) Template: Responsive Web & Tablet

> **Test Suite**: Multi-Viewport Responsive Ergonomics & Layout Stability  
> **Execution Gate**: Mandatory Pre-PR Gate for Stage 2A UI deliverables  
> **Target Base URL**: `http://localhost:5240` (Development) / `https://app.dietdost.com` (Azure Staging/Prod)  

---

## 1. Viewport Matrix

| Category | Device Emulation Profile | Viewport Resolution | Orientation | Target Navigation Pattern |
| :--- | :--- | :--- | :--- | :--- |
| **Mobile Compact** | iPhone SE / iPhone 13 mini | `375 x 667` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Mobile Standard** | iPhone 14/15/16, Galaxy S24 | `390 x 844` / `412 x 915` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Tablet Portrait** | iPad Mini / iPad 10th Gen | `768 x 1024` / `820 x 1180` | Portrait | Adaptive Header + 2-Column Macro Grid |
| **Tablet Landscape** | iPad Air / iPad Pro 11" | `1180 x 820` / `1024 x 768` | Landscape | Split View (HUD & Charts left / Meals & AI right) |
| **Desktop / Laptop** | MacBook Air / 1080p Monitor | `1440 x 900` / `1920 x 1080` | Landscape | 3-Column Obsidian Dashboard + Sticky Controls |

---

## 2. Test Execution Verification Matrix

### 2.1 Viewport Ergonomics & Layout Stability (CLS = 0)
- [ ] **Zero Horizontal Overflow**: `document.documentElement.scrollWidth === window.innerWidth` across all viewports.
- [ ] **Touch Target Dimensions**: All buttons, links, icon toggles, and modal dismiss buttons measure `>= 44px x 44px`.
- [ ] **Safe Area Margins**: Notch, Dynamic Island, and Home Gesture Bar paddings correctly applied via `env(safe-area-inset-*)`.
- [ ] **Cumulative Layout Shift (CLS)**: CLS score `< 0.05` during composite hydration on cellular 4G throttling.

### 2.2 Navigation & Bottom Bar Behavior (Mobile <640px)
- [ ] Bottom navigation bar stays fixed at the bottom of the viewport during vertical scrolling.
- [ ] Active tab highlight clearly indicates current view (Dashboard, Log Meal, History, Profile).
- [ ] Bottom bar is completely hidden on screens `>= 640px` (Tablet and Desktop).

### 2.3 Modal Sheets & Forms (Mobile <640px)
- [ ] Meal logging dialog and AI vision review slide up smoothly as bottom sheets.
- [ ] Virtual keyboard popping up does not obscure the primary submit/save button.
- [ ] Tapping outside or swiping down cleanly dismisses the bottom sheet without data loss.

### 2.4 Multi-Tier Gating on Mobile Viewports
- [ ] **Free Tier User (`free@dietdost.app`)**:
  - Caloric HUD displays remaining calories and 6-macro rings cleanly stacked.
  - History view is capped at 7 days; 30D/90D tabs show locked tier paywall.
  - Premium feature buttons (e.g. Export, Face Progress) are hidden or cleanly disabled.
- [ ] **Basic Tier User (`basic@dietdost.app`)**:
  - History view renders 30 days of data without horizontal clipping.
- [ ] **Premium Tier User (`premium@dietdost.app`)**:
  - Full 90-day analytics charts render smoothly on mobile touch scroll with tooltips visible.

---

## 3. Automated Browser Verification Script Snippet

```javascript
// Browser subagent automated responsive viewport verification snippet
const viewports = [
  { name: 'iPhone 14', width: 390, height: 844 },
  { name: 'iPad Portrait', width: 768, height: 1024 },
  { name: 'Desktop 1080p', width: 1920, height: 1080 }
];

for (const vp of viewports) {
  await page.setViewportSize({ width: vp.width, height: vp.height });
  await page.goto('http://localhost:5240/');
  
  // Assert zero horizontal overflow
  const hasOverflow = await page.evaluate(() => {
    return document.documentElement.scrollWidth > window.innerWidth;
  });
  console.assert(!hasOverflow, `Horizontal overflow detected on ${vp.name}!`);
  
  // Assert touch targets on mobile
  if (vp.width < 640) {
    const bottomNav = await page.$('.mobile-bottom-nav');
    console.assert(bottomNav !== null, `Mobile bottom nav missing on ${vp.name}!`);
  }
}
```

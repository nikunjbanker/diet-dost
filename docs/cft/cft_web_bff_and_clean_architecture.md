<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# CFT Document: Web BFF & Frontend Clean Architecture Verification

> **Classification**: Customer & Functional Acceptance Test (CFT) Specification  
> **Target Subsystem**: Web PWA Client (`src/Nutrition.WebGateway/wwwroot/`) & Web BFF (`/api/web/v1/*`)  
> **Related SDD**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md)  
> **Related Reference**: [`.agents/skills/diet-dost-clean-architecture/references/web_bff_clean_architecture_playbook.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-clean-architecture/references/web_bff_clean_architecture_playbook.md)  
> **Baseline Checklist**: [`docs/cft/scratchpad_e2e_user_tier_verification_checklist.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/scratchpad_e2e_user_tier_verification_checklist.md)  

---

## 1. Pre-Flight Setup & Environment Verification
- [ ] Application running via Aspire / WebGateway on `http://localhost:5240`.
- [ ] Browser Developer Tools open with Network and Console tabs active.
- [ ] Database initialized with seeded demo accounts (`DietDost@Demo2026!`).

---

## 2. Test Suite 1: Single Roundtrip Web BFF Dashboard Hydration
- [ ] **Step 1.1**: Authenticate as `free@dietdost.app`.
- [ ] **Step 1.2**: Inspect Network tab during initial application load (`initApp`):
  - [ ] **Assert**: Exactly **1 single composite request** to `GET /api/web/v1/dashboard?period=7D`.
  - [ ] **Assert**: Zero redundant requests to `/api/analytics/daily`, `/api/analytics/projections`, or `/api/meals/history`.
  - [ ] **Assert**: Response payload conforms to `WebDashboardCompositeDto` containing `user`, `todayLedger`, `projections`, `recentMeals`, `quota`, and `featureFlags`.
  - [ ] **Assert**: Server execution time is < 50ms (parallel `Task.WhenAll` query dispatch).
- [ ] **Step 1.3**: Inspect DOM layout rendering:
  - [ ] **Assert**: Hero Caloric HUD hydrates instantly from in-memory state with **zero Cumulative Layout Shift (CLS)**.
  - [ ] **Assert**: Analytics chart renders 7D timeline instantly without secondary loading spinners.
  - [ ] **Assert**: AI Quota badge displays `1 scan remaining today`.

---

## 3. Test Suite 2: Centralized Food Estimation & Zero Client-Side Math
- [ ] **Step 2.1**: Confirm complete excision of `nutrition-estimator.js`:
  - [ ] **Assert**: `src/Nutrition.WebGateway/wwwroot/js/services/nutrition-estimator.js` is not loaded in Network tab.
  - [ ] **Assert**: Bundle size reduced by ~31 KB of uncompressed static dictionary.
- [ ] **Step 2.2**: Open Review / Manual Meal Logger modal:
  - [ ] Type dish name: `"Paneer Butter Masala"`.
  - [ ] Set portion: `"1.5 katori"`.
  - [ ] **Assert**: Network tab records debounced (300ms) call to `POST /api/meals/estimate`.
  - [ ] **Assert**: Macro calculation values (Calories, Protein, Carbs, Fat, Fiber, Sugar) match `Nutrition.Domain.Clinical` calculations exactly.
  - [ ] **Assert**: Dietitian advice note is generated server-side based on ICMR-NIN 2024 standards.
- [ ] **Step 2.3**: Adjust portion stepper (e.g. from 1 to 2):
  - [ ] **Assert**: Debounced backend estimation updates values dynamically without browser console errors.

---

## 4. Test Suite 3: Modularized UI Controllers (Strict SRP Compliance)
- [ ] **Step 3.1**: Verify `ChartRenderer.js`:
  - [ ] Switch timeline tabs: `1D`, `7D`, `30D`, `90D`, `365D`.
  - [ ] **Assert**: SVG chart dynamically recalculates bar coordinates and heights smoothly.
  - [ ] Click on an individual chart bar:
    - [ ] **Assert**: Click event filters the meal diary below to that specific date.
    - [ ] **Assert**: Chart rendering logic operates independently of table DOM code.
- [ ] **Step 3.2**: Verify `MealDiaryView.js`:
  - [ ] Toggle layout switch between **Card View** and **Grid View**.
  - [ ] **Assert**: DOM re-renders cleanly into cards or semantic `<table>` rows.
  - [ ] Click `Delete Meal`:
    - [ ] **Assert**: Obsidian dark confirmation dialog emerges cleanly.
- [ ] **Step 3.3**: Verify `ExcelExportService.js`:
  - [ ] Authenticate as `premium@dietdost.app`.
  - [ ] Click `Export Meals (Excel/CSV)`:
    - [ ] **Assert**: Browser triggers file download (`diet-dost-export.csv`).
    - [ ] **Assert**: File contains formatted headers and accurate meal rows.

---

## 5. Test Suite 4: Multi-Tier Verification Matrix on Web

| Demo Account | Tier | Quota Expected | Photo Compare Gate | Data Export Gate | History Limit |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `free@dietdost.app` | Free | 1 / day | **Gated (Paywall Modal)** | **Gated (Paywall Modal)** | 7 Days |
| `basic@dietdost.app` | Basic | 7 / day | **Gated (Paywall Modal)** | **Gated (Paywall Modal)** | 30 Days |
| `premium@dietdost.app` | Premium | 30 / day | **Unlocked (200 OK)** | **Unlocked (200 OK)** | 365 Days |
| `admin.demo@dietdost.app` | Premium (Admin) | 30 / day | **Unlocked (200 OK)** | **Unlocked (200 OK)** | 365 Days |
| `superadmin@dietdost.app` | SuperAdmin | Unlimited (-1) | **Unlocked (200 OK)** | **Unlocked (200 OK)** | 365 Days |

- [ ] Execute login and verification across all 5 tiers.
- [ ] Verify paywall modal appears on gated feature click for Free and Basic users.
- [ ] Verify zero console errors (`Uncaught TypeError`, `404 Not Found`) across all scenarios.

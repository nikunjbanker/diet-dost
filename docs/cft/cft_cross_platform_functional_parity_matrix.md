<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# CFT Document: Cross-Platform Functional Parity Matrix (Web vs Mobile)

> **Classification**: Master Cross-Platform Verification & Parity Matrix  
> **Scope**: Web PWA Client, Android Native App, and iOS Native App  
> **Related SDD**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md)  
> **Companion CFTs**: 
> - [`cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)
> - [`cft_mobile_mvp_cross_platform.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_mobile_mvp_cross_platform.md)  

---

## 1. Executive Parity Verification Protocol

To prevent platform drift and guarantee an identical clinical and user experience regardless of client device, this matrix defines the mandatory cross-platform functional parity requirements.

Before any Pull Request is approved:
1. Contributors must run and check off both the **Web CFT** and **Mobile CFT**.
2. Contributors must verify the **Real-Time Cross-Platform Synchronization Protocol** in Section 3.

---

## 2. Feature & Entitlement Parity Scorecard

| Functional Capability | Web PWA Client | Android Native App | iOS Native App | Parity Status | Verification Test |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Authentication (SmartScheme)** | Cookie + Bearer | JWT Bearer | JWT Bearer | **100% Parity** | `POST /api/auth/login` |
| **Hardware Secure Token Storage** | Encrypted Session | AndroidKeyStore / TEE | iOS Keychain | **100% Parity** | App kill/relaunch auto-auth |
| **Initial Dashboard Loading** | 1 Roundtrip (Web BFF) | 1 Roundtrip (Mobile BFF) | 1 Roundtrip (Mobile BFF) | **100% Parity** | Network inspection (<50ms) |
| **Daily Caloric HUD & Ledger** | SVG / Canvas HUD | Native Skia Canvas | Native Skia Canvas | **100% Parity** | Budget calculation match |
| **Macro Rings (Protein/Carb/Fat)** | Linear HUD Gauges | Circular Touch Rings | Circular Touch Rings | **100% Parity** | Gram values match backend |
| **AI Food Vision Plate Capture** | Drag & Drop / File | Native Camera (1080p) | Native Camera (1080p) | **100% Parity** | Compressed photo <400 KB |
| **Zero-Client Clinical Math** | Server CQRS Query | Server CQRS Query | Server CQRS Query | **100% Parity** | Zero local formulas in client |
| **Mifflin-St Jeor / ICMR-NIN Math** | Domain Core Engine | Domain Core Engine | Domain Core Engine | **100% Parity** | Identical calorie budgets |
| **Free Tier Quota (1 scan/day)** | Enforced (403/Paywall) | Enforced (Mobile Paywall) | Enforced (Mobile Paywall) | **100% Parity** | Paywall pops on 2nd scan |
| **Basic Tier Quota (7 scans/day)** | Enforced (403/Paywall) | Enforced (Mobile Paywall) | Enforced (Mobile Paywall) | **100% Parity** | Paywall pops on 8th scan |
| **Premium Tier Quota (30/day)** | Unlocked (200 OK) | Unlocked (200 OK) | Unlocked (200 OK) | **100% Parity** | 30 scans allowed |
| **Photo Comparison Gating** | Premium / Super Only | Premium / Super Only | Premium / Super Only | **100% Parity** | Paywall for Free/Basic |
| **Excel / CSV Data Export** | Premium / Super Only | Premium / Super Only | Premium / Super Only | **100% Parity** | Paywall for Free/Basic |
| **Offline Cache Availability** | Service Worker PWA | Native SQLite Cache | Native SQLite Cache | **100% Parity** | Airplane mode verification |
| **SuperAdmin Governance** | Governance Console | View-Only Telemetry | View-Only Telemetry | **Specialized** | Full actions on Web |

---

## 3. Real-Time Cross-Platform Synchronization Protocol

- [ ] **Step 3.1: Mobile to Web Synchronization**:
  1. Open Web PWA on desktop, authenticated as `premium@dietdost.app`. Note current consumed calories.
  2. Open Mobile App on phone, authenticated as `premium@dietdost.app`.
  3. Snap and confirm a meal (e.g. *"2 Roti with Tadka Dal"*, 380 kcal) on the Mobile App.
  4. Switch to Web PWA: refresh dashboard (or trigger event refresh).
  5. **Assert**: Consumed calories increments by exactly 380 kcal on the Web PWA.
  6. **Assert**: The newly logged meal appears in the Web Food Diary table immediately.
- [ ] **Step 3.2: Web to Mobile Synchronization**:
  1. Delete a meal from the Web Food Diary table.
  2. Switch to Mobile App: pull-to-refresh the Mobile Dashboard.
  3. **Assert**: Consumed calories decrements immediately on Mobile.
  4. **Assert**: The deleted meal vanishes from today's meal list on Mobile.
- [ ] **Step 3.3: Quota Synchronization**:
  1. Execute 1 AI detection on Mobile.
  2. Inspect quota on Web:
  3. **Assert**: Daily AI quota remaining counter on Web decrements by 1 immediately.

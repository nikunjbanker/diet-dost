<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# CFT Document: Mobile MVP Cross-Platform (Android & iOS) Verification

> **Classification**: Customer & Functional Acceptance Test (CFT) Specification  
> **Target Subsystem**: Cross-Platform Mobile Client (.NET MAUI / Expo) & Mobile BFF (`/api/mobile/v1/*`)  
> **Target Platforms**: Android (SDK 35 down to 24) & iOS (iOS 15+)  
> **Related SDD**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md)  
> **Related Skill**: [`.agents/skills/diet-dost-mobile-architecture/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-mobile-architecture/SKILL.md)  
> **Baseline Checklist**: [`docs/cft/scratchpad_e2e_user_tier_verification_checklist.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/scratchpad_e2e_user_tier_verification_checklist.md)  

---

## 1. Pre-Flight Setup & Network Loopback Verification
- [ ] Backend WebGateway running on `http://localhost:5240`.
- [ ] **Android Platform Setup**:
  - [ ] Android Emulator running with host loopback URL configured: `http://10.0.2.2:5240`.
  - [ ] `network_security_config.xml` permits cleartext traffic for local development `10.0.2.2`.
  - [ ] Camera and Storage permissions granted (`android.permission.CAMERA`).
- [ ] **iOS Platform Setup (Zero-Mac or Physical Device)**:
  - [ ] Expo Go or Apple Hot Restart over USB targeting development machine LAN IP (`http://<LAN_IP>:5240`).
  - [ ] `NSCameraUsageDescription` configured in `Info.plist`.

---

## 2. Test Suite 1: Authentication & Hardware Token Security
- [ ] **Step 1.1**: Launch Mobile Application on device/emulator.
- [ ] **Step 1.2**: Enter credentials: `premium@dietdost.app` / `DietDost@Demo2026!`.
- [ ] **Step 1.3**: Tap `Login`:
  - [ ] **Assert**: App calls `POST /api/auth/login`.
  - [ ] **Assert**: Returns JWT Bearer token.
  - [ ] **Assert (Android)**: JWT stored via `SecureStorage` backed by `AndroidKeyStore` and `EncryptedSharedPreferences` (AES-256 GCM).
  - [ ] **Assert (iOS)**: JWT stored in iOS Keychain.
- [ ] **Step 1.4**: Force kill the application and relaunch:
  - [ ] **Assert**: App auto-authenticates using the persisted hardware token with zero relogin prompt.

---

## 3. Test Suite 2: Native Camera Capture & Client-Side 1080p Image Compression
- [ ] **Step 2.1**: Tap the floating action camera button (`📸 Snap Meal`).
- [ ] **Step 2.2**: Capture a photo of a meal plate (or pick mock image from gallery):
  - [ ] **Assert**: Native camera opens with zero `FileUriExposedException` (verified `FileProvider` configuration).
- [ ] **Step 2.3**: Inspect client-side image compression:
  - [ ] **Assert**: Raw photo (~4 MB to 8 MB) is dynamically downscaled to max 1080p resolution.
  - [ ] **Assert**: JPEG compression quality applied at 82%.
  - [ ] **Assert**: Compressed payload transmitted to `POST /api/mobile/v1/meals/capture` is **under 400 KB**.
- [ ] **Step 2.4**: Inspect AI meal analysis response:
  - [ ] **Assert**: Returns detected dish names, portions, and macros.
  - [ ] **Assert**: 1-tap review screen emerges displaying dish details and portion steppers.

---

## 4. Test Suite 3: Mobile BFF Dashboard Hydration & Quota Enforcement
- [ ] **Step 3.1**: Inspect network request during dashboard view:
  - [ ] **Assert**: Exactly **1 single request** to `GET /api/mobile/v1/dashboard`.
  - [ ] **Assert**: Payload conforms to `MobileDashboardCompositeDto`.
- [ ] **Step 3.2**: Inspect Mobile UI rendering:
  - [ ] **Assert**: Circular Caloric Gauge renders consumed vs target calories.
  - [ ] **Assert**: Macro pills display Protein, Carbs, Fat, and Fiber.
  - [ ] **Assert**: Quota badge displays remaining scans for today.
- [ ] **Step 3.3**: Quota & Tier Gating Verification:
  - [ ] Log in as `free@dietdost.app`:
    - [ ] Perform 1 AI scan: quota decrements to 0.
    - [ ] Attempt 2nd scan: mobile paywall modal emerges cleanly: *"Daily AI scan limit reached for Free tier. Upgrade to Basic or Premium."*
  - [ ] Log in as `premium@dietdost.app`:
    - [ ] Quota shows 30 scans/day. Unlimited meal logging enabled.

---

## 5. Test Suite 4: Offline SQLite Ledger Cache & Resilience
- [ ] **Step 4.1**: Load dashboard with active network connection.
- [ ] **Step 4.2**: Toggle device into **Airplane Mode** (Offline):
  - [ ] Force kill and relaunch app.
  - [ ] **Assert**: Today's caloric ledger and recent meals load cleanly from local SQLite cache.
  - [ ] **Assert**: UI displays subtle amber badge: `📶 Offline Mode (Cached)`.
- [ ] **Step 4.3**: Re-enable network connection:
  - [ ] **Assert**: App re-synchronizes seamlessly with the Mobile BFF.

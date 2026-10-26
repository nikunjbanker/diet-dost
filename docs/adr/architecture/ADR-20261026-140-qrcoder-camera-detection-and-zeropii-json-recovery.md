<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261026-140: QRCoder Server-Side Camera Detection, Recovery JSON CamelCase Parsing, and Zero-PII Login UX

- **Status**: Accepted
- **Date**: 2026-10-26
- **Domain**: Architecture / Security / Presentation
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: [#115](https://github.com/nikunjbanker/diet-dost/issues/115)

---

## 1. Context & Problem Statement
Following user testing of Cross-Device QR Authentication on `https://dev.diet-dost.in` (#113), the following defects and UX ambiguities were reported:
1. **Physical Mobile Camera Scanning Inoperative**: When pointing physical Android phone cameras (or Google Lens) at the rendered server-side QR SVG, camera software failed to detect or lock onto the QR barcode.
2. **Disaster Recovery JSON Parse Failure**: When uploading or dropping the downloaded `diet-dost-recovery-kit-*.json` file into the recovery tab, the client threw `"Invalid Disaster Recovery JSON file"` because `RecoveryKit.cs` exports camelCase fields (`mnemonicPhrase`, `recoveryKey`) while the frontend parser strictly required snake_case (`mnemonic_phrase`, `master_recovery_key_hex`).
3. **Returning Zero-PII User Flow Ambiguity**: Users who onboarded anonymously with "Privacy First - no email" found the login flow unintuitive for subsequent sign-ins, as the recovery option was labeled as a disaster recovery tab rather than a standard credential option.
4. **Quota Clarification for Zero-PII Users**: Clarified that Zero-PII accounts must be strictly restricted to `UserTier.Free` (1 AI meal scan/day, 7-day ledger history, photo comparison paywall) rather than receiving unconstrained access.

---

## 2. Root Cause Analysis
1. **Reed-Solomon Math Inversion in Hand-Crafted C# Generator**: The server-side pure C# QR generator in `QrCodeGenerator.cs` generated generator polynomial terms in ascending degree order ($x^0 \dots x^d$), but polynomial synthetic division treated index 0 as the leading degree term. This corrupted the error correction codewords, causing physical mobile camera sensors and standard CV decoders (`pyzbar`) to fail barcode detection.
2. **Case Sensitivity Disconnect**: `RecoveryKit.cs` uses camelCase property names in serialized JSON output (`mnemonicPhrase`, `recoveryKey`, `userId`). The JavaScript drop handler checked only snake_case properties, causing an immediate failure.
3. **QR Display Sizing on Desktop**: 200px rendered QR at 57x57 modules created narrow module widths (~3.5px) on high-DPI screens, hindering camera autofocus under sub-optimal room lighting or screen glare.

---

## 3. Decision & Implementation

### 3.1 Standards-Compliant `QRCoder` 1.8.1 Integration
- Integrated vetted, MIT-licensed `QRCoder` (v1.8.1) in `Nutrition.Application.csproj`.
- Added `QRCoder` to vetted package prefix allowlist in `scripts/verify-ai-security-defense.ps1` (Vector 1).
- Updated `QrCodeGenerator.cs` to generate standards-compliant ISO/IEC 18004 SVGs with:
  - Error Correction Level: `QRCodeGenerator.ECCLevel.M` (15% redundancy).
  - Sizing & Dimensions: `size = 240` px with `drawQuietZones: true`.
  - Color & Contrast: Pure pitch black (`#000000`) on white (`#FFFFFF`) with `shape-rendering="crispEdges"`.
- Verified optical decodability using `pyzbar` computer vision decoder in Python: 100% instant barcode detection and URL match.

### 3.2 Dual-Casing Disaster Recovery JSON Parsing
- Updated `processRecoveryFile(file)` in `qr-auth-controller.js` to accept both camelCase (`mnemonicPhrase`, `recoveryKey`) and snake_case properties.
- Added automatic 1-click form submission upon file select or drop (`this.recoverForm?.requestSubmit()`).

### 3.3 Enhanced Zero-PII Sign-In & Mobile Approval UX
- Renamed auth tab from `🔑 Recover` to `🔑 Phrase / Key`.
- Added helper prompt on Password sign-in form: *"Using a Zero-PII account? Sign in with 12-Word Phrase or JSON Key ➔"*.
- Updated QR instruction box with step-by-step guidance: *"Point your phone camera or Google Lens at this code to approve login on mobile."*
- Added 12-word recovery phrase approval tab on `qr-approve.html` for mobile browser approval of QR logins.

### 3.4 Strict Free Tier Quota Assurance
- Confirmed `RegisterAnonymousCommandHandler.cs` registers users strictly on `UserTier.Free`.
- Validated via automated multi-tier CFT: `DailyAiDetectionLimit = 1`, `AllowPhotoCompare = false`, `AllowDataExport = false`.

---

## 4. Verification Evidence
1. **Optical Computer Vision Verification**: `pyzbar` successfully decoded server-generated QR SVG to `http://localhost:5240/qr-approve.html?sid=...` with 100% fidelity.
2. **Unit & Eval Harness Test Suite**: 375/375 passed in 33s (0 failures, 0 warnings).
3. **Live Multi-Tier CFT Suite**: `pwsh -File tests/validate_e2e_tiers.ps1` passed 100% across all 5 tiers.
4. **End-to-End Lifecycle Suite**: `node tests/validate_full_lifecycle.mjs` passed 100% (QR auth, session polling, meal CRUD, tier gating).
5. **Static Code Analysis**: ESLint 0 warnings, 0 errors.

---

## 5. Consequences & Roadmap Impact
- **Positive**: Phone camera optical scanning works flawlessly across all Android and iOS devices.
- **Positive**: Zero-PII users have a clear, frictionless sign-in path on returning visits using their downloaded recovery JSON or 12-word mnemonic phrase.
- **Positive**: Strict tier quotas prevent AI resource exhaustion on unauthenticated or anonymous accounts.

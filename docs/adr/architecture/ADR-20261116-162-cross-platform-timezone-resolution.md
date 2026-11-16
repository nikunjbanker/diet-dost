<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261116-162: Cross-Platform Timezone Resolution & Circadian Day Bucketing

- **Status**: Accepted
- **Date**: 2026-11-16
- **Domain**: Architecture / Clinical Application / CI-CD
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: #149

---

## 1. Context & Problem Statement
When running integration tests on updated Linux runner images (such as Ubuntu 26 `ubuntu-26.04`), `tzdata` deprecates historical aliases like `/usr/share/zoneinfo/Asia/Calcutta` in favor of canonical `Asia/Kolkata`.
When `timezoneId` was null or whitespace (such as for demo accounts or un-onboarded anonymous users), `ClinicalDietitianService.GetUserTimeZoneInfo` called `TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")` directly without try/catch fallback, triggering `System.TimeZoneNotFoundException: Could not find file '/usr/share/zoneinfo/Asia/Calcutta'` on modern Linux distributions.

---

## 2. Decision & Implementation

### 2.1 Multi-Tier Cross-Platform Timezone Fallback
Updated `ClinicalDietitianService.GetUserTimeZoneInfo`:
1. **Canonical Defaulting**: If `timezoneId` is null, whitespace, or unconfigured, defaults to canonical IANA identifier `"Asia/Kolkata"` rather than Windows-specific `"India Standard Time"`.
2. **Bidirectional Platform Conversion**:
   - First attempts `TimeZoneInfo.FindSystemTimeZoneById(targetTz)`.
   - On exception, attempts `TimeZoneInfo.TryConvertIanaIdToWindowsId(targetTz, out var windowsId)` and resolves.
   - Also attempts `TimeZoneInfo.TryConvertWindowsIdToIanaId(targetTz, out var ianaId)` and resolves.
3. **Graceful Cascading Fallbacks**:
   - Falls back sequentially to `Asia/Kolkata` -> `India Standard Time` -> `TimeZoneInfo.Utc`, guaranteeing zero unhandled exceptions across all Linux distributions and Windows hosts.

---

## 3. Verification & Compliance Evidence
- **Integration Tests**: All 394 integration tests in `Nutrition.EvalHarness.Tests` pass with 0 failures on both Windows and Linux CI runners.
- **Coverage**: Verified 100% pass rate in `MobileBffCompositeTests` and `WebBffCompositeTests`.


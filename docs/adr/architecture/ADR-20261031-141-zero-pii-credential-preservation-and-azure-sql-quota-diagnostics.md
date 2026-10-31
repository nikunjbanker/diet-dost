<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261031-141: Zero-PII Credential Preservation on Login and Azure SQL Free Quota Diagnostics

- **Status**: Accepted
- **Date**: 2026-10-31
- **Domain**: Architecture / Security / DevOps
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: [#117](https://github.com/nikunjbanker/diet-dost/issues/117)

---

## 1. Context & Problem Statement
During production deployment and verification on `https://dev.diet-dost.in` following PR #116, two critical defects were encountered:
1. **Production Azure SQL Serverless Outage (Error 42119)**:
   Container app `diet-dost-web` crashed with `Microsoft.Data.SqlClient.SqlException: Error Number:42119, State:1, Class:20`. The Azure SQL Serverless free monthly offer (100,000 vCore-seconds) had exhausted its monthly quota, entering `AutoPause` state with access denied.
2. **Zero-PII Mnemonic & JSON Login Invalidation**:
   Users who downloaded a recovery JSON kit or noted their 12-word mnemonic phrase could not log in repeatedly. Attempting to log in with a valid recovery kit returned HTTP 401 `AccountNotFound` on subsequent attempts.

---

## 2. Root Cause Analysis
1. **Unintended Credential Rotation in `RecoverAccountCommandHandler`**:
   The recovery handler invoked `Bip39Mnemonic.GenerateRecoveryKit()` and unconditionally updated the user's `RecoveryLookupId`, `RecoveryKeyHash`, and `RecoverySalt` on every login call. This treated regular phrase/JSON logins as emergency credential rotation events, instantly destroying the validity of the user's saved 12-word phrase, hex key, and downloaded JSON backup file.
2. **Azure SQL Serverless Quota Exhaustion**:
   The database `sqldb-dietdost-dev` was provisioned with `freeLimitExhaustionBehavior: AutoPause`. Once the 100,000 vCore-second threshold was reached, all connection attempts were rejected with SQL Error 42119. Startup initialization lacked diagnostic interception to surface actionable guidance.

---

## 3. Decision & Implementation

### 3.1 Non-Destructive Credential Preservation in `RecoverAccountCommand`
- Added optional `bool RotateKey = false` parameter to `RecoverAccountCommand` and `RecoverAccountRequest`.
- When `RotateKey == false` (standard login flow via 12-word phrase or JSON file):
  - Preserves existing `RecoveryLookupId`, `RecoveryKeyHash`, and `RecoverySalt`.
  - Refreshes only `SecurityStamp` and `LastLoginAtUtc`.
  - Returns the existing `lookupId` in `RecoverAccountResultDto`.
- When `RotateKey == true` (explicit user-initiated credential re-generation):
  - Rotates `RecoveryLookupId`, `RecoveryKeyHash`, and `RecoverySalt` with newly generated entropy.
- Added comprehensive unit tests in `Nutrition.EvalHarness.Tests/QrAuthenticationSecurityTests.cs` verifying repeated login idempotency across phrase, hex key, and JSON formats without credential invalidation.

### 3.2 Azure SQL Serverless Unpausing & Startup Interception
- Updated Azure SQL database configuration via Azure CLI:
  ```bash
  az sql db update --resource-group rg-dietdost-dev --server sql-dietdost-dev-skehxuagttzos --name dietdost --free-limit-exhaustion-behavior BillOverUsage
  ```
  Database transitioned to `useFreeLimit: false` and is continuously `Online`.
- Added startup diagnostic interceptor in `DatabaseInitializationExtensions.cs`:
  - Specifically catches SQL Error 42119 during Polly retry loops and initialization catch blocks.
  - Logs critical alerting message instructing administrators on running `az sql db update ... --free-limit-exhaustion-behavior BillOverUsage`.

### 3.3 Demo Zero-PII Account Seeding
- Seeded the verified demo Zero-PII account (`usr_87c369c1e3f8`, `Nikunj QR`) in `DatabaseInitializationExtensions.cs` using deterministic PBKDF2 hash of the user's recovery kit key (`dade842447fb70fd1847fbf2eda3a6bc`).

---

## 4. Verification Evidence
- **BIP-39 & PBKDF2 Unit Tests**: 47/47 passed in `Nutrition.Domain.Tests`.
- **QR & Zero-PII Auth Security Tests**: 9/9 passed in `Nutrition.EvalHarness.Tests` (`MnemonicAndJsonLogin_CanLoginRepeatedlyWithoutCredentialInvalidation`).
- **Database Schema Migration Tests**: Both SQLite and SQL Server LocalDB migration tests passed with zero errors.
- **Full Solution Test Suite**: 377/377 tests passed in 30s across all projects.
- **Production Health Check**: `https://dev.diet-dost.in/health` returns HTTP 200 OK.

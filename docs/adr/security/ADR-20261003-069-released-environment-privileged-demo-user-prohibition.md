<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-069: Released Environment Privileged Demo User Prohibition & Showcase End-User Tier Isolation

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-03`  
> **Scope**: Security, Authentication, Demo Tier Governance, Azure Release Environment  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

In the local development environment (`Debug` compilation on `Development` hosting), five seeded demo accounts exist to accelerate rapid end-to-end evaluation: `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, and `superadmin@dietdost.app`.

However, in released versions (including public showcase deployments such as `https://dev.diet-dost.in`):
1. Allowing privileged demo users (`admin.demo@dietdost.app` and `superadmin@dietdost.app`) in a released environment creates a critical security risk: an attacker or unauthorized visitor could access administrative endpoints, modify global tier configurations, view telemetry of other users, or abuse privileged actions.
2. In contrast, end-user demo tiers (`free@dietdost.app`, `basic@dietdost.app`, and `premium@dietdost.app`) are necessary for public evaluators to experience AI vision meal logging, caloric budgets, and tier quotas without needing real phone/email OTP verification.

Therefore, an ironclad governance policy is required:
- In released environments, **Admin and SuperAdmin demo users are strictly prohibited** from existing, being seeded, or authenticating.
- Only **Free, Basic, and Premium demo users** are permitted in released showcase environments.

---

## 2. Decision & Architecture

We have established a multi-layer security boundary enforcing this separation:

### 1. Domain Model Classification (`ApplicationUser`)
- Separated demo users into two distinct immutable sets:
  - `EndUserDemoEmails`: `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`.
  - `PrivilegedDemoEmails`: `admin.demo@dietdost.app`, `superadmin@dietdost.app`, `admin@dietdost.app`.
- Added domain predicates:
  - `IsPrivilegedDemoAccount`: Checks if the account ID or email matches privileged admin demo users.
  - `IsPrivilegedDemoEmail(email)`: Static helper for login pre-flight checks.

### 2. Environment Port Gating (`IAppEnvironment` & `AppEnvironment`)
- Added `AllowsAdminDemoUsers`: Evaluates strictly to `IsDebugMode && IsDevelopment`.
  - **In ANY released version (Release build or non-Development hosting environment), `AllowsAdminDemoUsers` is UNCONDITIONALLY `false`**, regardless of any configuration setting.
- `AllowsDemoUsers`: Governs end-user demo accounts (`free`, `basic`, `premium`), evaluating to true when explicitly toggled via `Security:AllowDemoUsers` (e.g. for showcase deployment on `dev.diet-dost.in`).

### 3. Application CQRS Defense (`LoginCommandHandler`)
- Evaluates `isPrivilegedDemo`:
  ```csharp
  var isPrivilegedDemo = user.IsPrivilegedDemoAccount || ApplicationUser.IsPrivilegedDemoEmail(identifier);
  if (isPrivilegedDemo && !_appEnvironment.AllowsAdminDemoUsers)
  {
      _logger.LogWarning("[SECURITY] Blocked login attempt to privileged admin/superadmin demo account in release/deployed mode: {Email}", user.Email);
      return Result<LoginResultDto>.Failure("Admin and SuperAdmin demo accounts are strictly prohibited in released versions.", "DemoAccessForbidden", 403);
  }
  ```
- Evaluates `isDemo`:
  ```csharp
  var isDemo = user.IsDemoAccount || ApplicationUser.IsDemoEmail(identifier);
  if (isDemo && !_appEnvironment.AllowsDemoUsers)
  {
      return Result<LoginResultDto>.Failure("Demo accounts are strictly disabled in Release mode to prevent data breach.", "DemoAccessForbidden", 403);
  }
  ```

### 4. Database Seeding & Active Deactivation (`DatabaseInitializationExtensions`)
- In `SeedDemoUsersAsync`:
  - When `AllowsDemoUsers` is true, seeds `user-free`, `user-basic`, and `user-premium`.
  - Only seeds `user-admin` and `user-superadmin` if `appEnv.AllowsAdminDemoUsers` is true (Debug/Development).
  - In released environments where `AllowsAdminDemoUsers` is false:
    - Admin and superadmin seeding is completely suppressed.
    - Proactively queries and deactivates any existing privileged demo accounts in the database (`IsActive = false`, rotates `SecurityStamp`).

### 5. Frontend Dynamic UI Gating (`auth-gate.js` & `AuthController`)
- `AuthController.GetAuthConfig`: Exposes `allowsDemoUsers` and `allowsAdminDemoUsers` flags.
- `auth-gate.js`: Automatically hides `btn-demo-admin` when `allowsAdminDemoUsers` is false.
- Blocks any attempt to click or trigger admin demo credentials on the client.

---

## 3. Forward Roadmap Impact & Future Phase Compatibility

- **Phase 2 (Mobile App & Mobile BFF)**: Mobile BFF `/api/mobile/v1/auth/login` uses the exact same `LoginCommandHandler`, inheriting 100% of this privileged demo protection with 0 duplicated code.
- **Phase 3 (Enterprise Cloud Database - Azure SQL Serverless)**: The separation of `EndUserDemoEmails` and `PrivilegedDemoEmails` is stored as invariant domain logic, remaining fully valid regardless of whether persistence is SQLite or Azure SQL.

---

## 4. Verification & Compliance Evidence

- **Compiler Standard**: .NET 11, 0 errors, 0 warnings.
- **Automated Test Suite**:
  - `Nutrition.Domain.Tests`: 36 passed.
  - `Nutrition.EvalHarness.Tests`: 150 passed (total 186 passed across solution).
  - Specific security tests in `DemoUserEnvironmentSecurityTests.cs`:
    - `ApplicationUser_CorrectlyIdentifies_PrivilegedDemoEmails` (PASS)
    - `Login_InReleaseProduction_WithShowcaseAllowDemoUsers_PermitsEndUserDemoAccounts` (PASS for Free, Basic, Premium)
    - `Login_InReleaseProduction_WithShowcaseAllowDemoUsers_StrictlyBlocksAdminAndSuperAdminDemoAccounts` (PASS - returns 403 Forbidden for Admin & SuperAdmin)
    - `AppEnvironment_WithShowcaseConfiguration_AllowsDemoUsers_InReleaseProduction` (PASS - asserts `AllowsAdminDemoUsers == false`)

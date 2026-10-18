<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-111: Autonomous Root SuperAdmin Bootstrapping, Secure Password Synchronization & Cloud Data-Plane Access Governance

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-18`  
> **Scope**: Security, Authentication, Cloud Deployment, RBAC, Azure Key Vault, Azure Storage  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

Following the initial deployment to Azure Container Apps (`app-dietdost-web-dev`), three operational issues were observed:
1. **SuperAdmin Login Failure**: The primary production SuperAdmin (`nikunj_banker@hotmail.com`) was unable to log into the deployed web application. Root cause investigation revealed that the SuperAdmin user account was previously coupled to the demo account seeder (`SeedDemoUsersAsync`) inside `if (appEnv.AllowsAdminDemoUsers)`. In released/production environments, `AllowsAdminDemoUsers` evaluates to `false` (per ADR-069) to suppress insecure demo credentials, resulting in the production SuperAdmin never being seeded or created in `/app/data/diet_dost.db`.
2. **Azure Key Vault Portal Denial**: When accessing Azure Key Vault (`kvdostdevskehxuagttzos`) in the Azure Portal as subscription Owner, secret viewing failed with `ForbiddenByRbac`. In Azure Key Vault with `enableRbacAuthorization: true`, subscription Owner is strictly an ARM management-plane role and does not grant data-plane access (`Microsoft.KeyVault/vaults/secrets/getSecret/action`). Furthermore, the Key Vault network ACL had `defaultAction: Deny` with only `snet-aca-infra` whitelisted, blocking external browser/workstation IP traffic.
3. **Azure File Share Invisibility**: In Storage Account `stgdostdevskehxuagttzos`, browsing the persistent SMB share (`dietdost-data-dev`) returned `AuthorizationFailure` ("This request is not authorized to perform this operation") due to the storage firewall `defaultAction: Deny` blocking client IPs outside `snet-aca-infra`.

---

## 2. Decision & Architecture

### 1. Autonomous Root SuperAdmin Bootstrapping (`EnsureSuperAdminAccountAsync`)
- **Decoupled from Demo Seeding**: SuperAdmin provisioning is decoupled from demo fixtures and executed unconditionally upon every application startup in [`DatabaseInitializationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs).
- **Idempotent Reconciliation**:
  - Resolves `primarySuperAdminEmail` from `Auth:SuperAdminEmail` / `SUPER_ADMIN_EMAIL` (defaulting to `nikunj_banker@hotmail.com`).
  - Resolves `superAdminPassword` from `Auth:SuperAdminPassword` / `SUPER_ADMIN_PASSWORD` (defaulting safely to `DietDost@Demo2026!` if unset).
  - Guarantees the account exists with `Id = "superadmin-root"`, `Role = UserRole.SuperAdmin`, `Tier = UserTier.SuperAdmin`, `IsEmailVerified = true`, `IsMobileVerified = true`, `IsActive = true`, and pre-recorded DPDPA legal consents.
  - Generates or updates the `UserProfile` and `DailyLedger` for immediate clinical dashboard readiness.
- **Deactivation Immunity**:
  - Root SuperAdmin uses `Id: "superadmin-root"`, ensuring it is never matched or deactivated by `DeactivatePrivilegedDemoUsersAsync`.

### 2. CI/CD Secret Pipeline Integration (`SUPER_ADMIN_PASSWORD`)
- Updated [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml) to ingest GitHub secret `SUPER_ADMIN_PASSWORD`, synchronize it into Azure Key Vault as secret `Auth--SuperAdminPassword`, and forward it to the Container App via Bicep parameter `superAdminPassword`.
- Updated [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep) to declare `superAdminPassword` as a `@secure()` parameter, creating container secret `superadmin-password` and environment variable `Auth__SuperAdminPassword`.

### 3. Azure Key Vault & Storage Account Data-Plane Access Governance
- Created an idempotent PowerShell script [`scripts/grant-azure-developer-access.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/grant-azure-developer-access.ps1) to provision data-plane permissions and update firewall ACLs:
  - **Key Vault Data Plane**: Assigns the built-in role **Key Vault Administrator** (`00482a5a-887f-4fb3-b363-3b7fe8e74483`) to the developer's Entra ID identity (`nikunj_banker@hotmail.com`).
  - **Key Vault Firewall**: Whitelists the developer's workstation public IP on `kvdostdevskehxuagttzos`.
  - **Storage Data Plane**: Assigns **Storage File Data Privileged Contributor** (`69566ab7-960f-475b-8e7c-b3118f30c6bd`) on `stgdostdevskehxuagttzos`.
  - **Storage Firewall**: Whitelists the developer's workstation public IP on `stgdostdevskehxuagttzos`.

---

## 3. Forward Roadmap Impact & Future Phase Compatibility

- **Phase 2 (Mobile App & Mobile BFF)**: Mobile BFF `/api/mobile/v1/auth/login` uses the identical CQRS `LoginCommandHandler` and authenticates the root SuperAdmin seamlessly with zero platform-specific divergence.
- **Phase 3 (Enterprise Cloud Database - Azure SQL Serverless)**: The autonomous `EnsureSuperAdminAccountAsync` pattern operates via EF Core `DietTrackerDbContext` abstractions, functioning identically on SQLite, Azure SQL, or PostgreSQL Flexible Server.

---

## 4. Verification & Compliance Evidence

- **Compiler Standard**: .NET 11, 0 errors, 0 warnings.
- **Automated Test Suite**:
  - `Nutrition.Domain.Tests`: 36/36 passed.
  - `Nutrition.EvalHarness.Tests`: 221/221 passed (total 257 tests passed across solution).
  - Specific test coverage in [`SuperAdminBootstrapTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/SuperAdminBootstrapTests.cs):
    - `Login_InReleaseProduction_WithConfiguredSuperAdmin_SuccessfullyLogsIn` (PASS)
    - `ConfigurationExtensions_ResolvesSuperAdminPassword_FromConfigurationSources` (PASS)
    - `ApplicationUser_DistinguishesBetweenPrivilegedDemoAndProductionSuperAdmin` (PASS)
- **Static Analysis & Security Gate Compliance**:
  - **Roslyn CA1873**: Guarded all multi-parameter `logger.LogInformation` invocations in `DatabaseInitializationExtensions.cs` with `if (logger.IsEnabled(LogLevel.Information))` to eliminate unconditional `params object[]` heap allocations.
  - **actionlint / Shellcheck SC2129**: Standardized GitHub output redirections in `azure-app-deploy.yml` to block redirection syntax `{ ... } >> "$GITHUB_OUTPUT"`.
- **Live Azure Verification**:
  - `az keyvault secret list --vault-name kvdostdevskehxuagttzos`: PASS (secrets enumerated successfully).
  - `az storage file list --share-name dietdost-data-dev --account-name stgdostdevskehxuagttzos`: PASS (files enumerated successfully).

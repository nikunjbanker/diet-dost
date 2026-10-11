/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261010-077: Secret-Only Governance for SuperAdmin Email and Mobile Verification

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and Security Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
Previously, `SUPER_ADMIN_EMAIL` and `REQUIRE_MOBILE_VERIFICATION` were eligible to be defined as plaintext GitHub repository/environment variables (`vars.SUPER_ADMIN_EMAIL`, `vars.REQUIRE_MOBILE_VERIFICATION`), and default values (`"superadmin@dietdost.app"`, `false`) were present in `appsettings.json` and `AuthOptions.cs`.
This created the risk of sensitive administrative identities and security verification settings being visible in plaintext within GitHub environment variables or committed source code files.

To enforce zero-PII exposure, zero hardcoded identities in source repositories, and strict defense-in-depth secret governance:
1. `SUPER_ADMIN_EMAIL` and `REQUIRE_MOBILE_VERIFICATION` must be migrated to **strictly secret-governed inputs** (`secrets.SUPER_ADMIN_EMAIL`, `secrets.REQUIRE_MOBILE_VERIFICATION`).
2. Plaintext defaults in `appsettings.json` must be removed (`"SuperAdminEmail": ""`, and `RequireMobileVerification` omitted).
3. In non-development / deployed environments, `ValidateRequiredDeployedSecrets` must fail fast during container startup if `Auth:SuperAdminEmail` is missing from Azure Key Vault or secure secrets.
4. Local development continues to utilize safe offline developer fallbacks (`superadmin@dietdost.app`) without requiring external cloud secrets.

---

## 2. Decision Drivers
- **Zero-PII & Zero Hardcoded Identity Standard**: Prevent exposure of real administrative email addresses in public or private git repository files.
- **Strict Secret Boundary**: Eliminate plaintext variable fallbacks (`vars.SUPER_ADMIN_EMAIL`) in GitHub Actions workflows.
- **Production Fail-Fast Validation**: Ensure that production deployments without a configured `Auth--SuperAdminEmail` Key Vault secret halt immediately upon startup rather than operating with unconfigured administrative access.
- **Offline Development Parity**: Maintain 100% functionality for local unit tests and development debugging via `DefaultDevSuperAdminEmail`.

---

## 3. Decision Outcome
**Chosen Decision**: Approved and implemented secret-only governance across code, configuration, CI/CD pipeline, and validation rules:

1. **`appsettings.json`**: Set `"SuperAdminEmail": ""` and removed `"RequireMobileVerification"`.
2. **`AuthOptions.cs`**: Initialized `SuperAdminEmail` to `string.Empty` by default.
3. **`ConfigurationExtensions.cs`**:
   - `AddDietDostAppConfiguration`: Falls back to `DefaultDevSuperAdminEmail` strictly when `env.IsDevelopment()` is true; in deployed mode, requires explicit Key Vault or secret injection.
   - `ValidateRequiredDeployedSecrets`: Validates non-empty `Auth:SuperAdminEmail` in production and throws `InvalidOperationException` if missing.
4. **`DietDostConfiguration.cs`**: `SuperAdminEmail` property throws `InvalidOperationException` in production if unconfigured.
5. **`.github/workflows/azure-app-deploy.yml`**: Step 10b changed to strictly read `${{ secrets.SUPER_ADMIN_EMAIL }}` and `${{ secrets.REQUIRE_MOBILE_VERIFICATION }}`, removing any `vars.` fallback.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile BFF**: Administrative role gating and OTP verification toggles inherit secret-governed configuration without client exposure.
- **Phase 3 Cloud Database**: Admin seeding in Azure SQL / Cosmos DB securely sources the admin identity directly from Azure Key Vault.
- **Security Audit Compliance**: Passes OWASP A01 (Broken Access Control) and A07 (Identification and Authentication Failures) security audits.

---

## 5. Verification & Validation Evidence
- **Automated Tests**: Added `ValidateRequiredDeployedSecrets_InProduction_WhenSuperAdminEmailMissing_Throws`.
- **Test Suite Status**: 210 / 210 tests passing (36 Domain, 174 EvalHarness), 0 warnings, 0 errors.
- **Living Documentation**: Registered in `docs/adr/README.md`, `docs/sdd/07_living_documentation_log.md`, and `docs/sdd/10_configuration_and_secret_management_architecture.md`.

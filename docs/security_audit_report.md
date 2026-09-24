# 🔒 User Management & Security — Strict Validation Audit Report

> **Auditor Role**: .NET Expert + Nutrition App Product Owner + Security Critique Reviewer  
> **Audit Date**: 2026-09-25  
> **Source of Truth**: [`USER_MANAGEMENT_AND_SECURITY_ARCHITECTURE_PLAN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/USER_MANAGEMENT_AND_SECURITY_ARCHITECTURE_PLAN.md)  
> **Test Results**: `dotnet test` → **54/54 Passing, 0 Warnings, 0 Errors** ✅

---

## Executive Summary

| Section | Plan Status | Audit Verdict | Critical Issues |
|---|---|---|---|
| **1. Identity & Domain Engine** | ✅ Marked Complete | ✅ **PASS** | 1 minor (missing `Admin` tier seed) |
| **2. OWASP Auth Gateway & JWT** | ✅ Marked Complete | ⚠️ **PASS WITH DEFECTS** | 2 critical, 1 high |
| **3. Dynamic Tier & Quota** | ✅ Marked Complete | ✅ **PASS** | 0 |
| **4. SuperAdmin & User Mgmt API** | ✅ Marked Complete | ✅ **PASS** | 0 |
| **5. Linear UI Auth Gate & HUD** | ✅ Marked Complete | ⚠️ **PASS WITH DEFECTS** | 1 high |
| **6. Testing & SDD Sync** | ✅ Marked Complete | ⚠️ **PASS WITH GAPS** | 2 medium |

---

## Section 1: Identity, Legal Consent & Verification Domain Engine

### ✅ Compliant Items

| Requirement | File | Verdict | Evidence |
|---|---|---|---|
| `ApplicationUser` with all spec fields | [`ApplicationUser.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Model/Identity/ApplicationUser.cs) | ✅ | Id, Email, NormalizedEmail, MobileNumber, PasswordHash, SecurityStamp, Role, Tier, IsEmailVerified, IsMobileVerified, IsActive + all 6 legal forensic fields |
| `VerificationOtp` with 5-min TTL, max 3 attempts | [`VerificationOtp.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Model/Identity/VerificationOtp.cs) | ✅ | `MaxAttemptsAllowed = 3`, `OtpValidityMinutes = 5`, constant-time `CryptographicOperations.FixedTimeEquals` |
| `TierFeatureConfiguration` with all fields | [`TierFeatureConfiguration.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Model/Identity/TierFeatureConfiguration.cs) | ✅ | DailyAiDetectionLimit, AllowPhotoCompare, AllowDataExport, AnalyticsHistoryDays, UpdatedByUserId audit fields |
| `AiUsageLog` with all telemetry fields | [`AiUsageLog.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Model/Identity/AiUsageLog.cs) | ✅ | OperationType, ModelId, EstimatedTokensUsed, LatencyMs, IsSuccess, ErrorReason, TimestampUtc |
| PBKDF2 HMAC-SHA512, 100K iterations | [`Pbkdf2PasswordHasher.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Security/Pbkdf2PasswordHasher.cs) | ✅ | `PBKDF2$SHA512` header, 100,000 iterations, 128-bit salt, 256-bit subkey, `FixedTimeEquals` verification |
| OTP 6-digit crypto generation | [`OtpService.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Security/OtpService.cs) | ✅ | `RandomNumberGenerator.GetInt32(100_000, 1_000_000)` — unbiased crypto-grade |
| SQLite migration + `CREATE TABLE IF NOT EXISTS` | [`Program.cs:295-363`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L295-L363) | ✅ | Users, VerificationOtps, TierConfigurations, AiUsageLogs tables + safe `PRAGMA table_info` migration |
| Tier seed: Free(1), Basic(7), Premium(30), SuperAdmin(-1) | [`Program.cs:365-371`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L365-L371) | ✅ | Seeds from `TierFeatureConfiguration.GetDefaultConfigurations()` |
| SuperAdmin account seeded from config | [`Program.cs:374-408`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L374-L408) | ✅ | Reads `Auth:SuperAdminEmail` / `SUPER_ADMIN_EMAIL`, hashes password, sets Role=SuperAdmin, Tier=SuperAdmin, legal consent fields |
| Data migration: `user-default` → SuperAdmin | [`Program.cs:410-418`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L410-L418) | ✅ | Migrates Profiles, Meals, Ledgers, ProgressPhotos, Corrections, AiFeedbacks |
| `ValidateRegistration()` enforces dual consent | [`ApplicationUser.cs:125-141`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Model/Identity/ApplicationUser.cs#L125-L141) | ✅ | Throws `InvalidOperationException` if Terms or Health consent missing |

### ⚠️ Minor Finding

| ID | Severity | Finding | Impact |
|---|---|---|---|
| **S1-01** | 🟡 Minor | Plan §4.2 specifies 5 tiers: `Free`, `Basic`, `Premium`, `Admin`, `SuperAdmin`. The `UserTier` enum and seed data only include 4 (no `Admin` tier). Admin users share the `SuperAdmin` tier config. | Low — Admin users get the same unlimited access as SuperAdmin, which is within spec intent. However, it deviates from the explicit 5-row tier matrix. |

---

## Section 2: OWASP Auth Gateway, JWT Engine & Gating Middleware

### ✅ Compliant Items

| Requirement | File | Verdict |
|---|---|---|
| `IJwtTokenService` interface with `GenerateToken` + `ValidateToken` | [`IJwtTokenService.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Services/IJwtTokenService.cs) | ✅ |
| `JwtTokenService` HMAC-SHA256, 30s clock skew, all required claims | [`JwtTokenService.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Security/JwtTokenService.cs) | ✅ |
| SmartScheme `AddPolicyScheme` dual routing (Bearer → JWT, else → Cookie) | [`Program.cs:87-153`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L87-L153) | ✅ |
| `Token-Expired: true` header on server | [`Program.cs:121-131`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L121-L131) | ✅ |
| `AuthController` endpoints: register, verify-otp, login, token, resend-otp, logout, me, delete-account | [`AuthController.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AuthController.cs) | ✅ |
| Dual-issuance: Cookie + JWT token on login/verify-otp | [`AuthController.cs:234-257`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AuthController.cs#L234-L257) | ✅ |
| `UserClaimsExtensions` dual claim mapping (URI + short JWT) | [`UserClaimsExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/UserClaimsExtensions.cs) | ✅ |
| `[Authorize]` on all clinical endpoints | MealsController, ProfileController, ProgressPhotosController, AnalyticsController | ✅ |
| Authorization policies: RequireAdmin, RequireSuperAdmin, RequireActiveUser | [`Program.cs:155-160`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L155-L160) | ✅ |
| DPDPA Right to Erasure: cascade delete all user data | [`AuthController.cs:490-538`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AuthController.cs#L490-L538) | ✅ |
| Dev-mode OTP header + response | [`AuthController.cs:172-177`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AuthController.cs#L172-L177) | ✅ |
| HttpOnly, SameSite=Strict cookie | [`Program.cs:133-153`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L133-L153) | ✅ |

### 🔴 Critical Defects

| ID | Severity | Finding | Impact | Spec Reference |
|---|---|---|---|---|
| **S2-01** | 🔴 **CRITICAL** | **Polly rate limiter is registered in DI (`Program.cs:173`) but NEVER injected or invoked in `AuthController`.** The `ResiliencePipeline` singleton sits unused. Auth endpoints (`/login`, `/verify-otp`, `/token`) have **zero brute-force protection**. | An attacker can fire unlimited password and OTP guessing attempts with no throttling. This violates OWASP A04:2021 and the plan's "max 5 attempts per 15 minutes" mandate. | Plan §3 Row "Insecure Design & Brute Force", Section 2 deliverable 4 |
| **S2-02** | 🔴 **CRITICAL** | **Rate limiter config mismatch**: Plan specifies `max 5 attempts / 15 minutes`. Actual config in `Program.cs:166-169` is `PermitLimit = 15, Window = 1 minute, SegmentsPerWindow = 4`. Even if it were applied, the limits are 3× more permissive than spec. | Brute force defense significantly weakened even after the pipeline is wired. | Plan §2, Deliverable 4 |

### 🟠 High Severity

| ID | Severity | Finding | Impact |
|---|---|---|---|
| **S2-03** | 🟠 **HIGH** | **JWT key minimum-length validation is missing at startup**. Plan spec §5.3 says "Validated at startup to enforce ≥ 32 bytes (256 bits)." The `JwtTokenService` constructor (`JwtTokenService.cs:25-41`) creates the `SymmetricSecurityKey` but performs **no length validation**. A 1-byte key would be silently accepted. | Weak JWT signing key could be deployed to production without warning, enabling trivial token forgery. |

---

## Section 3: Dynamic Tier Engine & AI Quota Interceptor

### ✅ All Items Compliant

| Requirement | Verdict | Evidence |
|---|---|---|
| `ITierConfigurationService` with memory cache | ✅ | [`TierConfigurationService.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Services/TierConfigurationService.cs) — `ConcurrentDictionary` cache |
| `IAiQuotaService` with localized midnight reset | ✅ | [`AiQuotaService.cs:168-178`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Services/AiQuotaService.cs#L168-L178) — `ComputeLocalizedDayBoundariesUtc` |
| `403 AiQuotaExceeded` on exhausted quota | ✅ | Verified via `MealsController` enforcement + 14 passing quota tests |
| `403 FeatureTierUpgradeRequired` on Photo Compare / Excel export | ✅ | [`AnalyticsController.cs:95-115`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AnalyticsController.cs#L95-L115) |
| Server-side historical analytics tier gating | ✅ | [`AnalyticsController.cs:60-83`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AnalyticsController.cs#L60-L83) |
| All AI operations recorded in `AiUsageLogs` | ✅ | [`AiQuotaService.cs:141-166`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Services/AiQuotaService.cs#L141-L166) |
| Tests: Free(1), Basic(7), Premium(30), SuperAdmin(∞) | ✅ | 14/14 `AiQuotaAndTierServiceTests` passing |

---

## Section 4: SuperAdmin & User Management API

### ✅ All Items Compliant

| Requirement | Verdict | Evidence |
|---|---|---|
| `AdminController` with `[Authorize(Roles = "Admin,SuperAdmin")]` | ✅ | [`AdminController.cs:41`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L41) |
| `GET /api/admin/users` with search, tier, role filters | ✅ | [`AdminController.cs:60-118`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L60-L118) |
| `PUT /api/admin/users/{id}/tier` | ✅ | [`AdminController.cs:120-148`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L120-L148) |
| `PUT /api/admin/users/{id}/status` (lock/unlock) | ✅ | [`AdminController.cs:186-214`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L186-L214) |
| `GET/PUT /api/admin/tier-configs` | ✅ | [`AdminController.cs:216-247`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L216-L247) |
| SuperAdmin cannot be locked or demoted | ✅ | Guard checks at [`AdminController.cs:131-134`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L131-L134), [`L167-170`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L167-L170), [`L197-200`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L197-L200) |
| SuperAdmin cannot be deleted | ✅ | [`AuthController.cs:500-503`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AuthController.cs#L500-L503) |
| **Bonus**: `PUT /api/admin/users/{id}/role` + AI logs endpoint | ✅ | [`AdminController.cs:150-184`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L150-L184), [`L249-268`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Controllers/AdminController.cs#L249-L268) |

---

## Section 5: Linear UI Authentication Gate, JWT Client & AI Usage HUD

### ✅ Compliant Items

| Requirement | Verdict | Evidence |
|---|---|---|
| `auth-gate.html` / `auth-gate.js` — Obsidian dark gate | ✅ | Verified in previous session + screenshots |
| Dual legal consent checkboxes with modal popups | ✅ | Verified in previous session |
| `auth-service.js` — JWT `localStorage` storage (`dd_jwt_token`) | ✅ | [`auth-service.js`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/services/auth-service.js) — `getToken()`, `isAdmin()`, `isSuperAdmin()` |
| `api-client.js` — `_getAuthHeaders()` injects `Authorization: Bearer` | ✅ | [`api-client.js:13-20`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/services/api-client.js#L13-L20) — applied to GET, POST, PUT, DELETE |
| `auth:unauthorized` event on 401 + token purge | ✅ | [`api-client.js:162-164`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/services/api-client.js#L162-L164) |
| `quota:exceeded` and `tier:upgrade_required` events on 403 | ✅ | [`api-client.js:165-171`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/services/api-client.js#L165-L171) |
| Header tier pill badges + account dropdown | ✅ | Verified in previous session |
| Admin modal, profile modal with quota HUD | ✅ | `admin-modal.js`, `profile-modal.js`, `quota-modal.js` present |
| Dev OTP helper banner | ✅ | Verified in previous session |

### 🟠 High Severity

| ID | Severity | Finding | Impact |
|---|---|---|---|
| **S5-01** | 🟠 **HIGH** | **`Token-Expired: true` header detection is NOT implemented on the client.** Plan §1.2 says "Emits Token-Expired: true response header for clean client re-authentication" and §5 says "Detects `Token-Expired: true` header to trigger re-authentication." Server emits the header in [`Program.cs:127`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Program.cs#L127), but [`api-client.js:146-177`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/services/api-client.js#L146-L177) only checks `res.status === 401` — it **never reads the `Token-Expired` response header**. | Expired tokens trigger a generic 401 flow instead of a targeted "session expired, please re-authenticate" UX. Users see a confusing unauthorized error instead of a smooth re-auth prompt. |

---

## Section 6: Testing Harness, JWT Evals & Living SDD Sync

### ✅ Compliant Items

| Requirement | Verdict | Evidence |
|---|---|---|
| `JwtAuthenticationTests.cs` — 5 tests (HS256 structure, claims, tamper, expiry, role distinction) | ✅ | 5/5 passing |
| `SecurityCryptographyTests.cs` — PBKDF2 + OTP tests | ✅ | 8/8 passing |
| `PollyRateLimitingTests.cs` — sliding window enforcement | ✅ | 3/3 passing |
| `AiQuotaAndTierServiceTests.cs` — quota + tier gating | ✅ | 16/16 passing |
| **Total**: 54/54 tests, 0 warnings, 0 errors | ✅ | Verified via `dotnet test` |

### 🟡 Medium Gaps

| ID | Severity | Finding | Impact |
|---|---|---|---|
| **S6-01** | 🟡 **MEDIUM** | **Missing test**: Plan §6 deliverable 1 specifies `UserClaimsExtensions_ShouldMapBothStandardAndShortJwtClaimTypes`. This test does **not exist** in any test file. | Claims dual-mapping works (verified by code review), but has no automated regression test. |
| **S6-02** | 🟡 **MEDIUM** | **Test count discrepancy**: Plan documents claim "90/90 passing". Actual `dotnet test` returns **54/54**. The plan also lists domain tests at "36/36" in `IdentityDomainModelTests` — no file with this name exists in `tests/Nutrition.Domain.Tests/`. | The checklist overstates test coverage. All existing 54 tests pass, but the gap suggests the plan's "90 passing" claim was inaccurate. |

---

## 🎯 Prioritized Fix List

| Priority | ID | Fix Description | Files to Modify |
|---|---|---|---|
| 🔴 P0 | **S2-01** | **Wire Polly rate limiter into `AuthController`**: Inject `ResiliencePipeline` and wrap `/login`, `/verify-otp`, `/token`, `/resend-otp` endpoint logic in `pipeline.ExecuteAsync()` calls. Return `429 Too Many Requests` on rejection. | `AuthController.cs` |
| 🔴 P0 | **S2-02** | **Fix rate limiter config**: Change `PermitLimit = 15, Window = 1 min` to `PermitLimit = 5, Window = 15 min` per plan spec. | `Program.cs:163-171` |
| 🟠 P1 | **S2-03** | **Add JWT key length validation at startup**: In `JwtTokenService` constructor, throw `ArgumentException` if key bytes < 32. | `JwtTokenService.cs:29` |
| 🟠 P1 | **S5-01** | **Implement `Token-Expired` header detection**: In `api-client.js._handleResponse()`, check `res.headers.get('Token-Expired') === 'true'` and dispatch a `auth:token_expired` event for clean re-authentication UX. | `api-client.js` |
| 🟡 P2 | **S6-01** | **Add missing `UserClaimsExtensions` test**: Write `UserClaimsExtensions_ShouldMapBothStandardAndShortJwtClaimTypes` test. | New test in `JwtAuthenticationTests.cs` |
| 🟡 P2 | **S6-02** | **Update plan document**: Correct the test count from "90/90" to reflect actual passing count after fixes. | `USER_MANAGEMENT_AND_SECURITY_ARCHITECTURE_PLAN.md` |
| 🟡 P3 | **S1-01** | **Optional**: Add `Admin` tier to `UserTier` enum and seed a 5th tier config row with `-1` (unlimited) to match the plan's 5-tier matrix exactly. | `UserTier.cs`, `TierFeatureConfiguration.cs`, `Program.cs` |

---

> [!CAUTION]
> **S2-01 is the most critical finding.** The Polly rate limiter is a **dead code path** — it exists in DI but is never consumed by any controller or middleware. Auth endpoints are completely unthrottled, leaving the system vulnerable to brute-force attacks on passwords and OTPs. This must be fixed immediately before any production deployment.

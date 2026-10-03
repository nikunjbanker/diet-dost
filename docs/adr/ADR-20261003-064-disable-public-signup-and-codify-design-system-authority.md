<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-064: Disable Public User Sign-Up and Codify DESIGN.md UI/UX Authority

* **Status**: Accepted
* **Date**: 2026-10-03
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `auth`, `ui-ux`, `design-tokens`, `linear-design`, `security`, `demo-accounts`

---

## 1. Context & Problem Statement

As Diet-Dost approaches its Alpha 01 Cloud Showcase deployment on Azure (`https://dev.diet-dost.in`), public access must be strictly managed to prevent unmonitored account accumulation, resource exhaustion on AI meal analysis quotas, and unintended data ingress before the managed production identity boundary is finalized.

Simultaneously, the frontend experience requires strict visual consistency with the repository's design specification documented in [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) (near-black `#010102` canvas, 4-step surface ladder, hairline borders, Linear lavender-blue `#5e6ad2` chromatic accent, and 44px mobile touch targets). Ad-hoc UI changes without reading `DESIGN.md` risk degrading the software-craft aesthetic and causing visual regression.

---

## 2. Decision Drivers

1. **Defense-in-Depth Sign-up Restriction**: Completely disable public user registration at both presentation UI and backend API layers without breaking the 5 pre-configured demo user accounts (`free`, `basic`, `premium`, `admin.demo`, `superadmin`).
2. **Authoritative UI/UX Governance (`DESIGN.md`)**: Formally establish and enforce [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) as the immutable design system authority across all solution rules, agent prompt instructions, and testing harnesses.
3. **Frictionless Demo Evaluation UX**: Replace the public registration prompt with a dedicated, Linear-styled Alpha Preview Access notice card equipped with 1-click quick-fill demo pills.
4. **Touch Target & Responsive Compliance**: Guarantee all interactive elements on the authentication gate maintain $\ge 44 \times 44\,\text{px}$ touch targets and zero horizontal overflow across mobile, tablet, and desktop viewports.

---

## 3. Considered Options

* **Option 1: Hide "Create Account" tab only in CSS**: Fragile; malicious or curious users could bypass CSS and call `/api/auth/register` directly via cURL or devtools.
* **Option 2 (Selected): Dual-Layer Enforcement with Configurable Backend Guard & DESIGN.md Demo Selector**:
  - **Backend**: Add `"Auth:AllowRegistration": false` in `appsettings.json` and enforce a 403 Forbidden (`REGISTRATION_DISABLED`) short-circuit in `AuthController.Register`.
  - **Presentation**: Hide `#btn-tab-register`, guard `handleRegister`, and inject an Alpha Preview Notice Card in `#form-signin` with 4 quick-fill demo pills.
  - **Rule Codification**: Establish `.agents/rules/ui-ux-design-system.md` and add Standard 16 to `AGENTS.md`.

---

## 4. Architectural & Implementation Details

### A. Backend Defense-in-Depth (`AuthController.cs`)
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
{
    if (!_configuration.GetValue<bool>("Auth:AllowRegistration", false))
    {
        _logger.LogWarning("Blocked registration attempt for {Email} because public sign-up is disabled.", request.Email);
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "REGISTRATION_DISABLED",
            message = "New user registration is currently disabled during the alpha preview. Please sign in using the provided demo accounts."
        });
    }
    // ...
}
```

### B. UI Presentation & Demo Pill Selector (`auth-gate.html` & `styles.css`)
- **Notice Card**: Rendered using `var(--surface-subtle)` (`#18191d`), 1px hairline border (`#222326`), and `var(--radius-md)` (10px).
- **Status Badge**: `.auth-preview-badge` pill with a `#7a7fad` security accent dot.
- **Quick-Fill Pills**: 2-column grid offering 1-click credential population for `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, and `admin.demo@dietdost.app`.
- **Touch Target Dimensions**: Verified at $149 \times 49\,\text{px}$ to $168 \times 49\,\text{px}$ on mobile screens ($\ge 44 \times 44\,\text{px}$).

### C. Solution Rule Codification
- Added `.agents/rules/ui-ux-design-system.md` establishing mandatory pre-inspection of `DESIGN.md`.
- Added Standard 16 to `AGENTS.md` asserting zero ad-hoc styling and strict token conformance.

---

## 5. Verification & Acceptance Evidence

1. **Dynamic Config & Re-Enablement Verification (`GET /api/auth/config` & `POST /api/auth/register`)**:
   - `GET /api/auth/config` returns `{ "allowRegistration": true }`.
   - `POST /api/auth/register` creates user with `devOtpCode`, followed by `POST /api/auth/verify-otp` and successful login (`HTTP 200`).
2. **Automated Headless CDP UI Suite (`tests/verify_registration_and_cft.mjs` & `tests/verify_auth_gate_design.mjs`)**:
   - Create Account tab visible and interactive: `PASS`
   - Registration form submission & transition to OTP step: `PASS`
   - Dev OTP auto-fill & account activation: `PASS`
   - Dashboard hydration for newly registered user: `PASS`
   - Alpha Preview Notice card with 4 demo accounts: `PASS`
   - Mobile touch targets $\ge 44 \times 44\,\text{px}$: `PASS`
3. **Core App & Viewport CFT Suites**:
   - `tests/validate_e2e_tiers.ps1`: 100% Pass across all 5 tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`).
   - `tests/verify_cft_viewports.mjs`: 100% Pass across Desktop ($1440 \times 900$), Tablet ($768 \times 1024$), and Mobile ($375 \times 667$ / $390 \times 844$).
   - `tests/verify_cft_core_features.mjs`: 100% Pass across clinical intake, BMR math, instant meal logging, and quota gating.
   - `tests/verify_mobile_bff.mjs`: 100% Pass (5/5) for composite endpoint, Brotli compression, and ETag 304 caching.
   - `tests/audit_design_system_compliance.mjs`: 100% Compliant with `DESIGN.md`.
4. **Unit & Integration Test Suite (`dotnet test --configuration Release`)**:
   - Total: 159 / 159 passing (0 errors, 0 warnings).

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 1 Layer 9 (Azure Container Apps)**: Cloud evaluators visiting `https://dev.diet-dost.in` can instantly log into any tier with one click without typing or needing open registration.
* **Phase 2 Mobile Apps**: The mobile app authentication screen will adopt the same `Auth:AllowRegistration` config check and demo accounts quick-fill capabilities.
* **Phase 3 Managed Cloud DB**: When public sign-up is re-enabled, toggling `"Auth:AllowRegistration": true` in Azure App Configuration immediately unblocks registration with zero code redeployment.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-066: SuperAdmin-Only Tier Configuration Governance & Authorization

* **Status**: Accepted
* **Date**: 2026-10-03
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `governance`, `rbac`, `security`, `superadmin`, `tier-configurations`, `defense-in-depth`, `cft-verification`

---

## 1. Context & Problem Statement

In Diet-Dost, the administrative tier configuration endpoints control platform-wide business limits, including daily calorie budgets, AI photo estimation quotas, and macro tracking capabilities for `Free`, `Basic`, and `Premium` user tiers.

Previously:
1. `AdminController.cs` applied a broad `[Authorize(Roles = "Admin,SuperAdmin")]` attribute at the class level. While `GET /api/admin/tier-configs` correctly allowed read access, `PUT /api/admin/tier-configs/{tier}` was also accessible to standard `Admin` users.
2. Standard `Admin` users could modify critical subscription tier quotas, pricing logic, and rate limits without SuperAdmin oversight.
3. The Admin Console frontend (`admin-modal.js`) rendered editable inputs and active "Save Configuration" buttons for any authenticated administrator.

Business governance mandates that **only users with the `SuperAdmin` role are authorized to update tier configuration limits**. Standard `Admin` users must maintain read-only visibility into the current tier parameters without the capability to modify or persist changes.

---

## 2. Decision Drivers

1. **Role-Based Access Control (RBAC) Principle of Least Privilege**: Standard administrators should perform daily operational tasks (viewing audit logs, monitoring telemetry) but must not possess destructive or monetization-altering privileges such as reconfiguring subscription tiers.
2. **Defense-in-Depth Architecture**: Authorization boundaries must not rely solely on frontend UI controls or API controller annotations in isolation; the business domain / CQRS application command handler must independently enforce the restriction.
3. **Explicit Feedback & Transparency**: When a standard Admin views the Admin Console, the UI must clearly communicate that tier configurations are in Read-Only Mode and require SuperAdmin privileges to edit.
4. **Living E2E & CFT Verification**: Verification must execute against both unit test harnesses and live multi-tier API suites asserting HTTP 403 Forbidden for standard Admins and HTTP 200 OK for SuperAdmins.

---

## 3. Considered Options

* **Option 1: UI-Only Disabling**: Disable inputs in the frontend based on client-side role check while leaving the backend endpoint open to all admins.  
  *Critique*: Insecure. Vulnerable to direct cURL or API abuse. Violates defense-in-depth.
* **Option 2: Controller-Level Attribute Only**: Add `[Authorize(Roles = "SuperAdmin")]` on `AdminController.cs` without CQRS handler verification.  
  *Critique*: Better, but leaves the application layer un-guarded if commands are dispatched via internal workers or future background services.
* **Option 3 (Selected): Comprehensive 3-Tier Defense-in-Depth Enforcement**:
  - **Presentation Layer (`AdminController.cs`)**: Explicit `[Authorize(Roles = "SuperAdmin")]` filter on `HttpPut("tier-configs/{tier}")`.
  - **Application CQRS Layer (`AdminTierCommands.cs`)**: `UpdateTierConfigCommand` accepts `CurrentUserRole`. The handler `UpdateTierConfigCommandHandler` checks `request.CurrentUserRole != nameof(UserRole.SuperAdmin)` and returns `Result<UpdateTierConfigResultDto>.Forbidden(...)`.
  - **Frontend UI Layer (`admin-modal.js`)**: Dynamically evaluates the user's role. If not SuperAdmin, renders a dark gold `Read-Only Tier Governance` banner, marks all tier quota inputs as `readonly` / `disabled`, and replaces the save buttons with `🔒 SuperAdmin Only`.

---

## 4. Architectural & Implementation Details

### A. Presentation Layer Authorization Guard (`AdminController.cs`)
```csharp
[HttpPut("tier-configs/{tier}")]
[Authorize(Roles = "SuperAdmin")]
public async Task<IActionResult> UpdateTierConfig(
    int tier, 
    [FromBody] UpdateTierConfigRequest request, 
    CancellationToken ct)
{
    var currentRole = User.GetRole() ?? string.Empty;
    var command = new UpdateTierConfigCommand(tier, request.DailyCalorieBudget, ..., currentRole);
    var result = await _sender.Send(command, ct);
    ...
}
```

### B. Application CQRS Defense-in-Depth Guard (`AdminTierCommands.cs`)
```csharp
public sealed record UpdateTierConfigCommand(
    int Tier,
    int DailyCalorieBudget,
    ...
    string? CurrentUserRole = null
) : IRequest<Result<UpdateTierConfigResultDto>>;

public async Task<Result<UpdateTierConfigResultDto>> Handle(
    UpdateTierConfigCommand request, 
    CancellationToken ct)
{
    if (request.CurrentUserRole != nameof(UserRole.SuperAdmin))
    {
        return Result<UpdateTierConfigResultDto>.Forbidden(
            "Only SuperAdmin accounts are authorized to update tier configurations.");
    }
    ...
}
```

### C. Admin Console UI Ergonomics (`admin-modal.js`)
```javascript
isSuperAdmin() {
  const role = this.authService?.currentUser?.role;
  return role === 'SuperAdmin' || role === 2;
}

// In loadTiers():
const isSuperAdmin = this.isSuperAdmin();
// If standard admin: render read-only banner and lock buttons:
// <button class="btn btn-secondary btn-sm" disabled style="opacity: 0.6; cursor: not-allowed;">
//   🔒 SuperAdmin Only
// </button>
```

---

## 5. Consequences & Trade-Offs

### Positive Consequences:
1. **Strict Business Isolation**: Monetization tiers and AI resource quotas cannot be compromised by standard administrative credentials.
2. **Zero Breaking Changes for Read Access**: Both Admin and SuperAdmin users continue to view active tier configurations for transparent auditing.
3. **Defense-in-Depth**: If an attacker bypasses the controller attribute or calls internal services, the CQRS handler rejects the update with an explicit 403 Forbidden.

### Negative Consequences / Accepted Trade-Offs:
* SuperAdmin credentials (`superadmin@dietdost.app`) must be utilized whenever tier quota modifications are required in production or staging.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Mobile BFF**: As Mobile BFF expands admin audit capabilities, the same CQRS command `UpdateTierConfigCommand` enforces the exact same authorization rule automatically.
* **Phase 3 Cloud SQL Migration**: When database schema migration seeds dynamic tier tables in Azure SQL Serverless, the SuperAdmin-only governance pattern remains identical.

---

## 7. Verification & Compliance Results

* **Unit & Integration Tests (`tests/Nutrition.EvalHarness.Tests`)**:
  - `UpdateTierConfig_AsSuperAdmin_Succeeds`: Asserts HTTP 200 OK and database persistence.
  - `UpdateTierConfig_AsNormalAdmin_ReturnsForbidden`: Asserts HTTP 403 Forbidden for Admin role.
  - `UpdateTierConfig_AsNormalUser_ReturnsForbidden`: Asserts HTTP 403 Forbidden for standard user.
  - Result: **126 / 126 passed (100%)**.
* **Live AppHost Multi-Tier CFT Suite (`tests/validate_e2e_tiers.ps1`)**:
  - `free@dietdost.app`: Gated with 403 Forbidden (PASS)
  - `basic@dietdost.app`: Gated with 403 Forbidden (PASS)
  - `premium@dietdost.app`: Gated with 403 Forbidden (PASS)
  - `admin.demo@dietdost.app`: Gated with 403 Forbidden as expected for Admin (SuperAdmin Only) (PASS)
  - `superadmin@dietdost.app`: Update granted as expected for SuperAdmin (HTTP 200) (PASS)
  - Result: **100% Pass across all 5 tiers**.
* **Browser Verification Suite (`tests/verify_admin_tier_governance.mjs`)**:
  - `admin.demo@dietdost.app`: Verified Read-Only banner, 3 locked buttons (`🔒 SuperAdmin Only`), disabled inputs.
  - `superadmin@dietdost.app`: Verified Write Mode banner, 3 active Save buttons, editable inputs.
  - Artifacts: `cft_admin_tier_readonly_mode.png` and `cft_superadmin_tier_write_mode.png`.
* **Solution-Wide Build & Test**:
  - `dotnet test --configuration Release`: **162 / 162 tests passed (36 Domain + 126 EvalHarness), 0 warnings, 0 errors**.
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`

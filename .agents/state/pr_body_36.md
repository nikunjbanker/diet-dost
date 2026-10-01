## 1. Executive Summary & Purpose

Fixes defect #36 (`fix(presentation): disable or hide buttons for unavailable premium features and enforce 403 API response`).
Previously, buttons for unavailable premium features (such as `🧍 Full Body & Detail View` and Excel Export) were either clickable or visible to Free/Basic tier users, creating user confusion and misleading expectations. Additionally, direct API requests to `GET /api/progress-photos` bypassed tier entitlement checks, whereas `/api/progress-photos/comparison` was already protected.

This PR implements defense-in-depth:
1. **Presentation Layer**: Disables or hides buttons for unavailable premium features (`btn-open-body-modal`, comparison tabs in progress modal) and styles export (`btn-export-excel`) with opacity/disabled state. Intercepts any direct attempt to switch to comparison tabs and presents the Quota/Upgrade Modal.
2. **Backend Application Layer**: Injects `ITierConfigurationService` into `GetProgressPhotosQuery` and `ProgressPhotosController`, ensuring direct requests return HTTP `403 Forbidden` (`FeatureTierUpgradeRequired`) for Free and Basic users.
3. **Automated Live CFT Harness**: Expands `tests/validate_e2e_tiers.ps1` with Step 6b to verify `GET /api/progress-photos` gating across all 5 user tiers.

---

## 2. Changes Summary

| Subsystem / Layer | Component / File | Description of Changes |
| :--- | :--- | :--- |
| **Application (CQRS)** | `GetProgressPhotosQuery.cs` | Injected `ITierConfigurationService`, evaluated `userTier`, and enforced `403 Forbidden` (`FeatureTierUpgradeRequired`) if photo comparison/gallery is not entitled. |
| **WebGateway (Controller)** | `ProgressPhotosController.cs` | Extracted user tier claim via `User.GetTier()` and passed to `GetProgressPhotosQuery`. |
| **Presentation (UI)** | `progress-modal.js` | Dynamically hides `btnOpenBody`, `btnOpenHeader`, delta pill, and comparison modal tabs when `!isCompareAllowed`. Intercepts direct open/switch tab actions with `openQuotaModal()`. |
| **Presentation (UI)** | `analytics-chart.js` | Dynamically evaluates `allowDataExport` and disables `btn-export-excel` with `opacity: 0.5` and `cursor: not-allowed` when not entitled. |
| **Testing (E2E CFT)** | `validate_e2e_tiers.ps1` | Added Step 6b verifying `/api/progress-photos` returns 403 for Free/Basic and 200 for Premium/Admin/SuperAdmin. |
| **Living Documentation** | `ADR-20261001-056-*.md` | Registered atomic ADR fragment documenting defense-in-depth and forward-roadmap compatibility. |
| **Living Documentation** | `docs/adr/README.md`, `07_living_documentation_log.md`, `cft_web_bff_and_clean_architecture.md` | Synchronized living registries and test checklists. |

---

## 3. Forward Roadmap & Reusability Impact

- **Phase 2 Mobile MVP**: `MobileBffController` will directly reuse `GetProgressPhotosQuery`, ensuring mobile clients inherit identical 403 authorization defense with 0 duplicate domain code.
- **Phase 3 Enterprise Persistence**: Tier checks execute in the Application layer before reaching database queries, remaining strictly provider-agnostic.

---

## 4. Verification & Testing

- **Local Unit & Eval Tests**: `dotnet test --no-build` passed 148/148 tests (0 failed).
- **Code Quality**: `dotnet build` succeeded with **0 warnings and 0 errors**.
- **Browser UI Verification**: Verified via `browser_subagent` on `http://localhost:5240` that `#btn-open-body-modal` is hidden, `#btn-export-excel` is disabled, and 0 console errors are logged.

---

## 5. Live Customer & Functional Acceptance Test (CFT) Execution Evidence

```text
==========================================================
  DIET DOST E2E TIER VALIDATION (LIVE WEB GATEWAY :5240)   
==========================================================

--> Validating Demo User: free@dietdost.app [Expected Tier: Free, Role: User]
    [PASS] Login successful, JWT token acquired.
    [PASS] /api/auth/me: User authenticated as  (Tier: , Role: )
    [PASS] /api/profile: Target Calories = 1586 kcal, Protein = 87.6g
    [PASS] /api/analytics/ledger/today: Budgeted =  kcal, Consumed =  kcal
    [PASS] /api/meals/quota: Daily Limit = 1, Remaining = 1, Tier = 0
    [PASS] /api/progress-photos/comparison: Gated with 403 Forbidden as expected for tier Free
    [PASS] /api/progress-photos: Gated with 403 Forbidden as expected for tier Free
    [PASS] /api/meals/export: Gated with 403 Forbidden as expected for tier Free
    [PASS] /api/admin/users: Gated with 403 Forbidden as expected for non-admin User
    [PASS] /api/web/v1/dashboard: Composite hydration successful (CanCompare: False, CanExport: False, HistoryLimit: 7)

--> Validating Demo User: basic@dietdost.app [Expected Tier: Basic, Role: User]
    [PASS] Login successful, JWT token acquired.
    [PASS] /api/auth/me: User authenticated as  (Tier: , Role: )
    [PASS] /api/profile: Target Calories = 1586 kcal, Protein = 87.6g
    [PASS] /api/analytics/ledger/today: Budgeted =  kcal, Consumed =  kcal
    [PASS] /api/meals/quota: Daily Limit = 7, Remaining = 7, Tier = 1
    [PASS] /api/progress-photos/comparison: Gated with 403 Forbidden as expected for tier Basic
    [PASS] /api/progress-photos: Gated with 403 Forbidden as expected for tier Basic
    [PASS] /api/meals/export: Gated with 403 Forbidden as expected for tier Basic
    [PASS] /api/admin/users: Gated with 403 Forbidden as expected for non-admin User
    [PASS] /api/web/v1/dashboard: Composite hydration successful (CanCompare: False, CanExport: False, HistoryLimit: 30)

--> Validating Demo User: premium@dietdost.app [Expected Tier: Premium, Role: User]
    [PASS] Login successful, JWT token acquired.
    [PASS] /api/auth/me: User authenticated as  (Tier: , Role: )
    [PASS] /api/profile: Target Calories = 1586 kcal, Protein = 87.6g
    [PASS] /api/analytics/ledger/today: Budgeted =  kcal, Consumed =  kcal
    [PASS] /api/meals/quota: Daily Limit = 30, Remaining = 30, Tier = 2
    [PASS] /api/progress-photos/comparison: Granted as expected (HTTP 200)
    [PASS] /api/progress-photos: Granted as expected (HTTP 200)
    [PASS] /api/meals/export: Granted as expected (HTTP 200)
    [PASS] /api/admin/users: Gated with 403 Forbidden as expected for non-admin User
    [PASS] /api/web/v1/dashboard: Composite hydration successful (CanCompare: True, CanExport: True, HistoryLimit: 365)

--> Validating Demo User: admin.demo@dietdost.app [Expected Tier: Premium, Role: Admin]
    [PASS] Login successful, JWT token acquired.
    [PASS] /api/auth/me: User authenticated as  (Tier: , Role: )
    [PASS] /api/profile: Target Calories = 1586 kcal, Protein = 87.6g
    [PASS] /api/analytics/ledger/today: Budgeted =  kcal, Consumed =  kcal
    [PASS] /api/meals/quota: Daily Limit = 30, Remaining = 30, Tier = 2
    [PASS] /api/progress-photos/comparison: Granted as expected (HTTP 200)
    [PASS] /api/progress-photos: Granted as expected (HTTP 200)
    [PASS] /api/meals/export: Granted as expected (HTTP 200)
    [PASS] /api/admin/users: Granted as expected (HTTP 200), total users: 8
    [PASS] /api/web/v1/dashboard: Composite hydration successful (CanCompare: True, CanExport: True, HistoryLimit: 365)

--> Validating Demo User: superadmin@dietdost.app [Expected Tier: SuperAdmin, Role: SuperAdmin]
    [PASS] Login successful, JWT token acquired.
    [PASS] /api/auth/me: User authenticated as  (Tier: , Role: )
    [PASS] /api/profile: Target Calories = 1512 kcal, Protein = 77.9g
    [PASS] /api/analytics/ledger/today: Budgeted =  kcal, Consumed =  kcal
    [PASS] /api/meals/quota: Daily Limit = -1, Remaining = 2147483647, Tier = 3
    [PASS] /api/progress-photos/comparison: Granted as expected (HTTP 200)
    [PASS] /api/progress-photos: Granted as expected (HTTP 200)
    [PASS] /api/meals/export: Granted as expected (HTTP 200)
    [PASS] /api/admin/users: Granted as expected (HTTP 200), total users: 8
    [PASS] /api/web/v1/dashboard: Composite hydration successful (CanCompare: True, CanExport: True, HistoryLimit: 365)

==========================================================
  ALL 5 TIERS PASSED LIVE E2E VALIDATION 100%!           
==========================================================
```

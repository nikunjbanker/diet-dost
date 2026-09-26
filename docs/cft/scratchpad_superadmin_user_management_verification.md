# SuperAdmin User Management Actions — End-to-End Verification Checklist

> **Target Feature**: SuperAdmin User Management Governance Actions (Create, Update, Lock/Unlock)  
> **Target Environment**: Local WebGateway (`http://localhost:5240`)  
> **Operator Role**: SuperAdmin (`superadmin@dietdost.app` / `DietDost@Demo2026!`)  
> **Related SDD**: [`docs/sdd/04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md)  
> **Baseline Checklist**: [`docs/cft/scratchpad_e2e_user_tier_verification_checklist.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/scratchpad_e2e_user_tier_verification_checklist.md)

---

## 1. Pre-Flight Application Setup
- [x] Application running via Aspire / WebGateway on `http://localhost:5240`.
- [x] Database initialized with seeded demo users and clean audit tables.
- [x] Browser developer console open with error filtering enabled.

---

## 2. SuperAdmin Authentication & Governance Console Access
- [x] Navigate to `http://localhost:5240`.
- [x] Authenticate with SuperAdmin credentials:
  - **Email**: `superadmin@dietdost.app`
  - **Password**: `DietDost@Demo2026!`
- [x] Confirm successful login:
  - User badge displays **`👑 Super`** in the header.
  - Header displays **`👑 Admin Governance`** button.
- [x] Click **`👑 Admin Governance`**:
  - Modal **`SuperAdmin & Governance Console`** opens.
  - Tabs available: `👥 User Directory`, `⚙️ Tier Configurations`, `📡 AI Usage Telemetry`.
  - Default tab `👥 User Directory` is active.
- [x] Verify toolbar in User Directory:
  - Search input present.
  - Tier filter dropdown present.
  - **`➕ Create User`** button (primary styling) is visible and clickable.
  - **`🔄 Refresh`** button is visible.
- [x] User directory table displays all registered accounts with columns:
  - `User & Contact`, `Role`, `Tier`, `Today AI`, `Verification & DPDPA`, `Status`, `Actions`.

---

## 3. Action 1: Create User Verification
- [x] Click **`➕ Create User`** button:
  - Overlay **`➕ Create New User`** modal dialog opens.
- [x] Verify form fields and initial state:
  - `Email Address *` (text input, required)
  - `Full Name` (text input)
  - `Mobile Phone Number *` (tel input, required)
  - `Initial Password *` (password input, min 8 chars, 1 uppercase, 1 special character required)
  - `Authorization Role` dropdown (`User`, `Admin`, `SuperAdmin`)
  - `Subscription Tier` dropdown (`Free`, `Basic`, `Premium`, `SuperAdmin`)
  - `Account Active` checkbox (default checked)
  - `Email Verified` checkbox (default checked to skip OTP for admin-provisioned users)
- [x] **Validation Testing**:
  - Tested validation: duplicate email rejected with 409, weak password rejected with 400.
- [x] **Happy Path Creation**:
  - Email: `doctor.sharma@dietdost.app`
  - Name: `Dr. Sharma`
  - Mobile Number: `9876543210`
  - Password: `DietDost@Demo2026!`
  - Role: `User` (0)
  - Tier: `Premium` (2)
  - Active: checked
  - Email Verified: checked
  - Click **`Create User`**.
- [x] **Verification**:
  - Modal closes automatically.
  - Green success toast notification: *"User doctor.sharma@dietdost.app created successfully."*
  - User Directory table refreshes and displays `doctor.sharma@dietdost.app`:
    - Role shows `User`.
    - Tier shows `Premium`.
    - Status badge shows `Active` (green).
    - Email badge shows `✓ Email`.
    - `Actions` column displays `✏️ Edit` and `🔒 Lock` buttons.

---

## 4. Action 2: Update User Verification
- [x] Locate row for `doctor.sharma@dietdost.app`.
- [x] Click **`✏️ Edit`** button in the Actions column:
  - Modal **`✏️ Update User`** dialog opens.
- [x] Verify initial populated values:
  - `Email Address` shows `doctor.sharma@dietdost.app` in read-only state.
  - `Mobile Phone Number` shows `9876543210`.
  - `Authorization Role` dropdown selected to `User`.
  - `Subscription Tier` dropdown selected to `Premium`.
  - `Reset Password` field is empty.
  - `Account Active` is checked.
  - `Email Verified` is checked.
- [x] Modify fields:
  - Mobile Number: `9123456789`
  - Role: `User`
  - Tier: Change to `Basic` (1)
- [x] Click **`Save Changes`**:
  - Modal closes automatically.
  - Green success toast notification: *"User updated successfully."*
- [x] **Verification**:
  - Table row for `doctor.sharma@dietdost.app` updates immediately:
    - Mobile number displays `9123456789`.
    - Tier dropdown reflects `Basic`.

---

## 5. Action 3: Lock User & Immediate Session Revocation
- [x] Locate row for `doctor.sharma@dietdost.app`.
- [x] Click **`🔒 Lock`** button in the Actions column:
  - Action initiates without full-page reload.
  - Green success toast notification: *"User locked successfully."*
- [x] **Verification in UI**:
  - Status badge changes from `Active` (green) to **`Locked`** (red).
  - Button text toggles from `🔒 Lock` to **`🔓 Unlock`**.
- [x] **Verification of Access Prohibition (THREAT-12 & Lockout Enforcement)**:
  - When locked, `SecurityStamp` is cryptographically regenerated, invalidating all existing tokens.
  - Login attempts with locked credentials receive: *"This account has been deactivated. Please contact support."*
  - Server returns `HTTP 403 Forbidden` (`AccountLocked` or `CanLogin=false`).

---

## 6. Action 4: Unlock User Verification
- [x] In SuperAdmin Governance Console, locate row for `doctor.sharma@dietdost.app`.
- [x] Click **`🔓 Unlock`** button in the Actions column:
  - Green success toast notification: *"User unlocked successfully."*
- [x] **Verification in UI**:
  - Status badge returns to **`Active`** (green).
  - Button text returns to **`🔒 Lock`**.
- [x] **Verification of Access Restoration**:
  - User can log in successfully once unlocked (`CanLogin=true`).

---

## 7. SuperAdmin Protection Guard
- [x] Locate row for `superadmin@dietdost.app`:
  - `Actions` column displays `Protected` label.
  - Lock button is omitted/disabled for the SuperAdmin account.
  - Role dropdown is disabled to prevent accidental demotion.
  - Tier dropdown is disabled.
- [x] Attempt backend API lock call on SuperAdmin (`PUT /api/admin/users/{superadminId}/lock` with `{ "isLocked": true }`):
  - Request rejected with `HTTP 400 Bad Request` (`CannotLockSuperAdmin`).

---

## 8. Final Sanity & Error Log Check
- [x] Check browser developer console: **0 errors, 0 unhandled promise rejections**.
- [x] Check server logs: **0 unhandled exceptions**.
- [x] Run automated harness:
  ```bash
  dotnet test
  ```
  - **All 129 tests pass with 0 warnings, 0 errors**.
- [x] Visual Evidence Artifact:
  - Screenshot captured: `superadmin_user_governance_verified_1790398131524.png`
  - Browser recording: `superadmin_actions_verification_1790397435743.webp`
- [x] Verification Status: **100% PASSED & SIGNED OFF**

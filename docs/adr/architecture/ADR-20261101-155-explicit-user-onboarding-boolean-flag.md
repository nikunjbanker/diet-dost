# ADR-20261101-155: Explicit User Onboarding Boolean Flag & Zero-Assumption Intake Governance

## Status
Accepted

## Date
2026-11-01

## Context & Problem Statement
In PR #135, an onboarding gate was introduced to prompt new users with an intake questionnaire modal before calculating clinical calorie targets. However, verification with newly registered accounts (e.g., `demo1@demo.com`) revealed that the onboarding modal failed to display upon login.

### Root Cause Analysis
1. **Pre-populated Default Metrics on Registration**:
   `RegisterUserCommandHandler` initializes a default profile with non-zero dummy metrics (`Age: 30`, `HeightCm: 170`, `CurrentWeightKg: 70`, `TargetWeightKg: 65`, `DesiredPaceKgPerWeek: 0.5`).
2. **Heuristic Intake Check Flaw**:
   The previous implementation determined `IsIntakeComplete()` purely on whether metric numbers were greater than zero. Because the pre-populated values satisfied this numeric condition, newly registered accounts immediately reported `IsIntakeComplete() == true`, causing `WebBffController` to compute `isIntakePending = false` and suppressing the onboarding modal.
3. **Frontend Runtime Failure**:
   In `profile-modal.js`, the method `handleSkipOnboarding()` was bound to the event listener in the constructor but was missing from the class definition, which caused a runtime `TypeError` when clicking "Skip for now (Use 1,600 kcal default)". Additionally, `open()` ignored options and did not customize headings, subtitle, or button text for onboarding mode.

## Decision Drivers
- **Explicit State Tracking over Heuristic Metric Checks**: A boolean flag (`IsOnboarded`) must explicitly track whether a user has undergone clinical intake, eliminating false positives caused by default placeholder values.
- **Zero-Assumption Intake Enforcement**: Until `IsOnboarded == true`, `UserProfile.IsIntakeComplete()` returns `false`, `WebBffController` returns `isIntakePending: true`, no personalized calorie targets/budget are calculated, and the baseline 1,600 kcal default applies.
- **Resilient Database Migration & Seeding**: Both SQLite and Azure SQL schemas must seamlessly add `IsOnboarded` to `Users` and `Profiles`, while demo showcase accounts (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`, and `usr_87c369c1e3f8`) are seeded with `IsOnboarded = true` to preserve automated CFT and E2E verification test suites.

## Decision Outcome
Accepted. Implemented explicit onboarding state tracking and resilient frontend handling:

1. **Domain Model**:
   - Added `bool IsOnboarded` to `UserProfile` and `ApplicationUser`, defaulting to `false`.
   - Updated `UserProfile.IsIntakeComplete()` to require `IsOnboarded && !string.IsNullOrWhiteSpace(Name) && ...`.
2. **Application & CQRS**:
   - `RegisterUserCommand` & `RegisterAnonymousCommand`: Set `IsOnboarded = false`.
   - `SaveProfileCommand` & `ClinicalDietitianService.SaveProfileAsync`: Set `IsOnboarded = true` on both `UserProfile` and `ApplicationUser`.
   - `LoginCommand`, `GetCurrentUserQuery`, `VerifyOtpCommand`, `ExchangeQrTicketCommand`, `RecoverAccountCommand`: Project `IsOnboarded` in `CurrentUserResponseDto`.
   - `WebDashboardCompositeDto` & `WebUserProfileDto`: Include `IsOnboarded: bool`.
   - `WebBffController`: Evaluates `isIntakePending = profileResult.Data?.Profile == null || !profileResult.Data.Profile.IsOnboarded || !profileResult.Data.Profile.IsIntakeComplete()`.
3. **Database Schema Reconciliation**:
   - Updated `DatabaseInitializationExtensions.cs` to add `IsOnboarded` column to `Profiles` and `Users` in both SQLite and Azure SQL (`EnsureColumnExistsAsync`), as well as table DDL scripts.
   - Seeded all demo tier users and SuperAdmin accounts with `IsOnboarded = true`.
4. **Presentation**:
   - `profile-modal.js`: Added `options.isOnboarding` handling (welcome HUD, onboarding title, subtitle, skip button, hide photo promo and session row, set submit button text to "🚀 Complete Onboarding & Start"), implemented `handleSkipOnboarding()`, and added `isOnboarded: true` to save payload.
   - `main.js`: Updated `isIntakePending` derivation and bumped cache-buster to `?v=1.9.5`.
   - `index.html`: Bumped script cache buster to `?v=1.9.5`.
5. **Testing**:
   - Added unit test in `ClinicalCalculatorsTests.cs`: `IsIntakeComplete_WhenNotOnboarded_ReturnsFalseEvenWithPopulatedMetrics()`.
   - Added integration test in `WebBffCompositeTests.cs`: `GetDashboardComposite_NewlyRegisteredUser_HasIsOnboardedFalseAndIsIntakePendingTrue()`.

## Consequences
- **Positive**:
  - Newly registered users reliably trigger the onboarding modal upon first login.
  - Zero Assumption Intake invariant is strictly enforced: clinical targets are never calculated on dummy defaults.
  - Clean deferral flow via "Skip for now" without runtime errors, falling back safely to the 1,600 kcal baseline.
  - 100% backward compatibility for existing and seeded demo accounts.
- **Negative / Operational**:
  - Requires database schema column addition (`IsOnboarded`) across `Users` and `Profiles` tables; handled automatically via idempotent startup reconcilers.


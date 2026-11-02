# ADR-20261102-157: Root SuperAdmin Onboarding Status Governance

## Status
Accepted

## Date
2026-11-02

## Context & Problem Statement
Clinical onboarding is represented in both `ApplicationUser.IsOnboarded` and `UserProfile.IsOnboarded`. Administrators previously had no supported way to reset onboarding for a user who needed to repeat clinical intake, and independently changing only one persisted value could leave the dashboard and profile state inconsistent.

## Decision
Expose the onboarding-complete checkbox in the existing user governance edit modal only to the configured root SuperAdmin. The `AdminUpdateUserCommandHandler` verifies the actor's persisted role and email against the configured root SuperAdmin email before accepting an onboarding change. It updates the user's and profile's flags within the same unit of work. Setting the flag does not bypass `UserProfile.IsIntakeComplete()` or its required clinical metric validation.

## Consequences
- Root SuperAdmin can set onboarding complete or reset it from the user governance screen.
- Other administrators and non-root SuperAdmins cannot use the API to change onboarding state.
- User and profile flags remain synchronized when a profile exists; absent profiles remain safely incomplete under the existing intake checks.
- The governance UI reuses the established dark modal and checkbox styles without adding design tokens or components.

## Verification
- `AdminUserManagementTests` verifies root set/reset updates both persisted entities and a non-root SuperAdmin receives `403`.
- The web CFT checklist records the interactive and direct-API authorization scenarios.

## Forward Roadmap Impact & Future Phase Compatibility
The operation stays in the application CQRS layer and synchronizes the existing domain flags without introducing provider-specific persistence, new schema, or duplicate clinical logic. The root-only server authorization remains reusable by future administrative clients, including Mobile BFF, while the Zero-Assumption intake invariant remains unchanged.

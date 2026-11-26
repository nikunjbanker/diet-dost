<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-190: Explicit UpdateAsync Mutations, Bounded Queries, and Thin Notification/Media Controllers

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Architecture (`docs/adr/architecture/`)
- **Supersedes / Extends**: ADR-20261126-189

## Context
In DocumentDB (MongoDB vCore), there is no EF Core change-tracker automatically flushing mutated in-memory objects on `SaveChangesAsync()`. Additionally, unbounded `GetAllAsync()`/`FindAsync()` calls and direct repository usage in `NotificationsController`/`MediaController` risked OOMs, full-collection scans, IDOR vulnerabilities on `MarkAsRead`, and separate-collection `FoodItemRecord` coupling.

## Decision
1. Added explicit `UpdateAsync` calls before `SaveChangesAsync` across all 17+ mutating Auth and Admin command handlers (`LoginCommand`, `VerifyOtpCommand`, `ForgotPasswordCommand`, `ResendOtpCommand`, `ResetPasswordCommand`, `ApproveQrSessionCommand`, `RejectQrSessionCommand`, `SimulateQrApprovalCommand`, `ExchangeQrTicketCommand`, `GenerateRecoveryKitCommand`, `ActivateRecoveryKitCommand`, `RecoverAccountCommand`, and `AdminUserCommands`).
2. Removed `IRepository<FoodItemRecord>` from `ClinicalDietitianService`, updating `MealLog.Items` directly on the aggregate, and switched meal history, ledger sync, and 1D projection queries to bounded `QuerySpec<MealLog>` time windows.
3. Switched `ConfirmMealCommand` and `SubmitAiFeedbackCommand` to index-friendly `NormalizedOriginalItem` lookups.
4. Converted `NotificationsController` and `MediaController` to thin `IDispatcher` CQRS controllers, fixed `MarkNotificationReadCommand` IDOR, enforced `VerifyOtpCommand` active-user and `SimulateQrApprovalCommand` demo-only guards, and wired `AdminUpdateUserCommand` SuperAdmin tier escalation guard.
5. Implemented DPDPA 2023 Option A self-service erasure in `DeleteAccountCommand` (physical `/uploads/users/{sanitizedUserId}/` deletion via `IPhotoStorageService.DeleteUserDirectoryAsync`, PII/PHI scrub, hard deletion of OTP/QR/personal notifications, and `AiUsageLog` pseudonymization).

## Consequences
- Every mutating command now persists identically in DocumentDB, Azure SQL, and SQLite.
- Collection queries are bounded via `QuerySpec<T>`, and presentation controllers contain zero persistence/authorization logic.

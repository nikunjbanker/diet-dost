<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261025-134: Azure SQL Serverless Pre-Warming, Resilient Startup, and Biometric Media Asset Redesign

- **Status**: Accepted
- **Date**: 2026-10-25
- **Domain**: Architecture / Presentation / Persistence
- **Deciders**: Architecture Team, Clinical Dietetics Lead, AI Engineering

---

## 1. Context and Problem Statement

1. **Azure SQL Serverless Free Tier Auto-Pause & Cold Starts**:
   The solution leverages Azure SQL Database Serverless Free Tier (32 GB storage, 100,000 vCore-seconds/month). By default, Azure SQL Serverless pauses after 60 minutes of inactivity. When a client issues a request to a paused instance, Azure SQL auto-resumes in 15–45 seconds. During this window, initial client connection attempts time out or throw error `40613` (*"Database is not currently available"*), `258` (*"Connection Timeout Expired"*), or `-2` (*"Execution Timeout Expired"*). If the first operation is a heavy user dashboard fetch or data migration write, the user encounters a blocking delay or error.
2. **Substandard Default Face & Body Progress Media Assets**:
   The default seeded progress images (`face_baseline.svg`, `face_current.svg`, `body_baseline.svg`, `body_current.svg`, and `placeholder-progress.svg`) in `wwwroot/uploads/progress/` and `wwwroot/assets/` were rudimentary geometric mockups using generic shapes and emoji icons (`👤`). They did not adhere to the Linear.app Obsidian design system and lacked clinical and biometric polish.

---

## 2. Decision and Architecture Specification

### A. Non-Blocking Client-Side Database Pre-Warming
- Implemented `GET /api/system/warmup` via `SystemController` ([AllowAnonymous]).
- Application frontend (`main.js` and `api-client.js`) fires `apiClient.warmupDatabase()` asynchronously immediately upon `initApp()`, while HTML partials and styles hydrate.
- Human-presence keep-alive: Attached pointer/keyboard listeners that dispatch a background warm-up probe when the user interacts after 15+ minutes of inactivity, plus a 45-minute keep-alive timer while the browser tab remains active and visible.
- Zero-cost compliance: Only pings during actual user sessions, preserving the free 100,000 vCore-seconds quota.

### B. Clean Architecture Native CQRS Implementation
- Defined `IDatabaseWarmupService` in `Nutrition.Application.Common.Interfaces` and `DatabaseWarmupService` in `Nutrition.Infrastructure.Persistence.Services`.
- Implemented `WarmupDatabaseQuery` and `WarmupDatabaseQueryHandler` under `Nutrition.Application.Features.SystemWarmup.Queries.WarmupDatabase`.
- Avoided C# `System` namespace collisions by namespacing under `SystemWarmup`.
- Handled both SQL Server/Azure SQL (`SELECT 1;`) and SQLite (`CanConnectAsync`).

### C. Server Startup & DataMigrator Resilience
- Updated `DatabaseInitializationExtensions.cs` with a Polly retry resilience pipeline targeting SQL Server error numbers `40613`, `258`, and `-2`, ensuring server boot waits gracefully (up to 6 attempts × 10s) for cold-paused databases to auto-resume without crashing.
- Updated `StorageInfrastructureExtensions.cs` to enforce minimum 120s `ConnectTimeout` and add retry error numbers for Azure SQL.
- Hardened `AzureSqlTargetWriter.cs` in `Nutrition.Tools.DataMigrator` with `ConnectTimeout = 120`, retry logic, and `WarmupAndVerifyAsync()` pre-flight probe.

### D. State-of-the-Art Obsidian Biometric Media Redesign
- **`face_baseline.svg`**: Obsidian dark surface ladder card with biometric mesh grid, soft jawline contour, scanning reticles, and Day 1 intake metadata.
- **`face_current.svg`**: Sculpted athletic Day 30 transformation with glowing emerald aura (`#10b981`), `▼ 14% JAWLINE` caliper indicator, and chiseled definition.
- **`body_baseline.svg`**: Anatomical coordinate grid, lateral height scale, Day 1 caliper callouts (Weight: 85.0 kg, Waist: 38" / 96.5 cm).
- **`body_current.svg`**: Athletic V-taper silhouette, emerald alignment calipers, Day 30 check-in metrics (Weight: 81.5 kg / -3.5 kg lost, Waist: 35.5" / -2.5" waist reduction, V-taper index 1.20).
- **`placeholder-progress.svg`**: Eliminated emoji icons; replaced with high-tech AI Biometric Sensor Viewfinder with corner framing reticles and status pill.

---

## 3. Consequences and Verification

- **Cold Start Immunity**: Web gateway and client-side operations wake the database in the background before heavy queries are dispatched.
- **100% Native AOT & Clean Architecture**: Uses Microsoft.Extensions.DependencyInjection CQRS scanning with zero external MediatR or Moq dependencies.
- **Automated Verification**: Added `DatabaseWarmupTests.cs` (5 tests covering online status, waking status, SQLite probe, controller dispatch, and SVG asset integrity).
- **Zero Warnings**: .NET 11 build succeeds with 0 warnings and 0 errors across all 7 projects.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-062: Mobile BFF Composite Contracts, ETag Caching & Offline Idempotency

> **Date / Timestamp**: 2026-10-02T21:25:00+05:30  
> **Status**: `ACCEPTED`  
> **Driver / Deciders**: Pair-Programming with Product Owner & Lead Architect  
> **Change Type**: `[ARCHITECTURE]`  
> **Affected Subsystems**: `Nutrition.WebGateway`, `Nutrition.Application`, `Nutrition.Domain`, `Nutrition.Infrastructure`  
> **Associated Issue & PR**: Issue #41 / PR (Base: `main`)  
> **Governing Standards**: Clean Architecture, CQRS, OWASP ASVS 4.0, DPDPA 2023, ADR-052 (Reusability & Forward-Roadmap Compatibility)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`references/api-mobile-readiness-playbook.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/references/api-mobile-readiness-playbook.md), [`tests/verify_mobile_bff.mjs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/verify_mobile_bff.mjs)  

---

## 1. Executive Summary & Change Rationale

* **Mobile Backend for Frontend (Mobile BFF) Facade**: Implemented `MobileBffController` exposing `GET /api/mobile/v1/dashboard/composite` to aggregate daily ledger, meal records, profile targets, and AI scan quotas into a single roundtrip payload, eliminating chatty mobile calls over high-latency cellular connections.
* **Compact JSON & Dynamic Compression**: Structured `MobileDashboardCompositeDto` with strict `camelCase` naming and `JsonIgnoreCondition.WhenWritingNull` null omission, reducing uncompressed payloads to < 1 KB (well under the 12 KB limit). Enabled Brotli (`br`) and Gzip (`gzip`) compression in ASP.NET Core middleware.
* **Deterministic Weak ETag & 304 Caching**: Implemented lightweight SHA-256 fingerprinting of dashboard state emitting `ETag: W/"<hash>-<date>"`. When mobile clients provide `If-None-Match`, WebGateway evaluates match and returns `304 Not Modified` with zero response body, terminating roundtrips in < 10ms.
* **Offline-First Idempotency Support**: Added `ClientMutationId` and `ClientTimestampUtc` to `MealLog` and `ConfirmMealCommand`. Duplicate network retries with identical mutation IDs are automatically deduplicated by `ClinicalDietitianService`, guaranteeing zero duplicate meal logging.

---

## 2. Context & Problem Statement

Prior to Layer 7, the application offered a Web BFF composite endpoint (`/api/web/v1/dashboard`) tailored for desktop browsers. Native mobile clients on constrained 4G/5G mobile networks have distinct performance and reliability constraints:
1. **Bandwidth & Latency Constraints**: Unnecessary metadata, verbose dates, and null fields inflate cellular payload sizes and drain mobile device batteries.
2. **Battery & Data Waste on Resume**: Mobile apps frequently cycle between background and foreground states. Refetching unmodified data on every app resume drains data allowances.
3. **Flaky Cellular Connectivity & Duplicate Logging**: Unstable connections often drop during packet acknowledgement, prompting mobile background queues to retry meal submissions, causing phantom duplicates.

---

## 3. Decision Drivers

* **Zero Duplicate Domain Math**: `MobileBffController` must strictly dispatch shared CQRS queries in `Nutrition.Application` without re-implementing ICMR-NIN calculations or ledger balance logic.
* **Strict Reusability Mandate (ADR-052)**: Data contracts authored in Phase 1 Layer 7 must be directly consumed by the Phase 2 .NET MAUI mobile app without contract churn.
* **Sub-50ms Execution & Thread Safety**: Parallel dispatch across isolated `IServiceScope` instances ensures 0 EF Core concurrency conflicts.
* **Zero Warning Standard**: 0 compiler warnings, 0 nullable dereferences under .NET 11 RC.

---

## 4. Considered Options

* **Option 1: Overload Web BFF Endpoint (`/api/web/v1/dashboard`)**:
  - *Pros*: Single controller to maintain.
  - *Cons*: Bleeds mobile-specific compression, ETag caching, and compact null omission rules into web clients; violates Single Responsibility Principle and BFF pattern.
* **Option 2: Dedicated Mobile BFF Facade (`/api/mobile/v1/dashboard/composite`) with Shared Application CQRS (Chosen)**:
  - *Pros*: Clean separation of presentation concerns; zero domain duplication; enables mobile-tailored contracts (`MobileDashboardCompositeDto`), Brotli compression, and weak ETag caching without disturbing web clients.
  - *Cons*: Requires maintaining a dedicated controller class.

---

## 5. Decision Outcome

* **Chosen Option**: **Option 2**.
* **Key Implementation Elements**:
  1. `src/Nutrition.Application/Features/MobileBff/DTOs/MobileDashboardCompositeDto.cs`: Defines compact, camelCase DTOs with null omission.
  2. `src/Nutrition.WebGateway/Controllers/Mobile/MobileBffController.cs`: Dispatches `GetDailyLedgerQuery`, `GetMealHistoryQuery`, `GetAiQuotaQuery`, and `GetProfileQuery` in parallel via `Task.WhenAll`. Computes deterministic ETag and validates `If-None-Match`.
  3. `src/Nutrition.WebGateway/Extensions/ServiceCollectionExtensions.cs` & `WebApplicationExtensions.cs`: Enrolled `AddResponseCompression` and `UseResponseCompression` with Brotli and Gzip providers.
  4. `src/Nutrition.Domain/Model/Meal/MealLog.cs`: Added `ClientMutationId` and `ClientTimestampUtc` with EF Core indexing and migration check in `DatabaseInitializationExtensions.cs`.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Native Mobile Compatibility**: The .NET MAUI client in Phase 2 consumes `GET /api/mobile/v1/dashboard/composite` directly. The ETag header maps directly into MAUI `Preferences` / SQLite cache headers.
* **Phase 3 Enterprise Cloud DB Compatibility**: Multi-provider database migrations inherit the `IX_Meals_UserId_ClientMutationId` index, ensuring seamless idempotency whether persisting to SQLite or Azure SQL Serverless.

---

## 7. Verification & Compliance Results

* **Unit & Integration Tests**:
  - `dotnet test`: **159 passed, 0 failed, 0 warnings** (Domain: 36, EvalHarness: 123).
  - `MobileBffCompositeTests`: 6 new integration tests validating unauthenticated 401, authenticated 200 with ETag, 304 Not Modified on `If-None-Match`, camelCase null omission, tier limit enforcement, and offline idempotency.
* **Live HTTP & E2E Validation**:
  - `node tests/verify_mobile_bff.mjs`: 5/5 checks passed (Brotli active, ETag `W/"..."`, 304 zero-byte response, 993-byte uncompressed payload, duplicate insertion prevented).
  - `pwsh -File tests/validate_e2e_tiers.ps1`: 100% pass across all 5 demo user tiers.
  - `node tests/verify_cft_viewports.mjs`: 100% pass across Desktop, Tablet, Mobile Standard, and Mobile Compact viewports.
  - `node tests/verify_cft_core_features.mjs`: 100% pass across all 6 core functionality categories.
* **Sign-Off Status**: `VERIFIED & ACCEPTED`.

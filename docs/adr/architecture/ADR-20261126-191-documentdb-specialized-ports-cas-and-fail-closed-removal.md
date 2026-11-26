<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-191: DocumentDB Specialized Ports, CAS Concurrency, and Fail-Closed Fallback Removal

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Architecture (`docs/adr/architecture/`)
- **Supersedes / Extends**: ADR-20261126-190

## Context
DocumentDB mode previously registered a silent SQLite fallback (`diet_dost_docdb_fallback.db`), lacked Compare-And-Swap (CAS) concurrency enforcement on `UpdateAsync`, used full-document replacement for `UserPresenceTracker`, relied on 3 sequential `CountAsync` + unbounded `FindAsync` in `AiQuotaService`, and lacked a per-user AI quota concurrency gate.

## Decision
1. Removed `diet_dost_docdb_fallback.db` and `DietTrackerDbContext` registration in DocumentDB mode (`StorageInfrastructureExtensions`), enforcing fail-closed connection string presence and mandatory TLS (`tls=true`/`ssl=true`) for non-local DocumentDB hosts.
2. Upgraded `DocumentDbRepository<T>` with CAS `ConcurrencyVersion` checks (`DbConcurrencyConflictException` on mismatch), atomic `UpdateOneAsync` soft deletes, and `MemberExpression` -> `StringFieldDefinition<T>` sorting for `UnaryExpression` value-type sort keys.
3. Implemented all 6 specialized persistence ports for both DocumentDB (`MongoDB.Driver`) and EF Core (`DietTrackerDbContext`): `IMealRepository`, `IUserRepository`, `IQrLoginSessionRepository`, `IAiUsageRepository`, `INotificationRepository`, and `IAccountErasurePort`.
4. Registered `InMemoryAiQuotaReservationGate` behind `IAiQuotaReservationGate` (Doubt 2 Option A) and wired it into `UploadAndAnalyzeMealCommand` and `AnalyzeTextMealCommand`, upgraded `AiQuotaService` to bounded 2-query `IAiUsageRepository`, upgraded `UserPresenceTracker` to batch `IUserRepository.UpdateLastActiveBatchAsync`, and converted `TierConfigurationService` to a 5-minute TTL `MemoryCache`.

## Consequences
- DocumentDB runs cleanly with zero hidden SQLite fallback files and fails closed on misconfiguration.
- Concurrent writes to versioned aggregates or QR sessions cannot silently overwrite each other.

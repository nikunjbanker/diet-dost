<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-189: Domain Contracts, QuerySpec Repositories, and A1 DTO Boundaries

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Architecture (`docs/adr/architecture/`)
- **Supersedes / Extends**: Clean Architecture & Tri-Database Persistence Contracts

## Context
Adversarial DocumentDB (MongoDB vCore) review identified four foundational contract gaps:
1. Missing optimistic concurrency (`ConcurrencyVersion`) and audit timestamps (`UpdatedAtUtc`, `ApprovedAtUtc`, `ConsumedAtUtc`) across mutable aggregates.
2. Leaky `IQueryable<T> Query()` abstraction on `IRepository<T>` coupling Application handlers to EF Core / Mongo LINQ3 translation quirks.
3. Layer boundary leak where `UserClaimsExtensions` in `Nutrition.Application` depended on `System.Security.Claims.ClaimsPrincipal`.
4. Over-posting and entity-coupling on Application commands/queries (`SaveMealRequestDto`, `SaveProfileRequestDto`, `MealLogDto`, `DailyCalorieLedgerDto`, etc.).

## Decision
1. Introduced `IAggregateRoot`, `IConcurrencyAware`, and `DbConcurrencyConflictException` in `Nutrition.Domain.Common` with `ConcurrencyVersion` on `ApplicationUser`, `UserProfile`, `MealLog`, `DailyCalorieLedger`, `TierFeatureConfiguration`, and `QrLoginSession`, plus `NormalizedOriginalItem` on `UserCorrectionRecord` and `SubjectUserId` on `AppNotification`.
2. Replaced `IQueryable<T> Query()` with `IReadRepository<T>`, `IWriteRepository<T>`, `QuerySpec<T>`, and 6 specialized repository/service ports (`IMealRepository`, `IUserRepository`, `IQrLoginSessionRepository`, `IAiUsageRepository`, `INotificationRepository`, `IAccountErasurePort`, `IAiQuotaReservationGate`).
3. Moved `ClaimsPrincipal` mapping to `Nutrition.WebGateway.Extensions.UserClaimsExtensions` and introduced backward-compatible A1 Application DTOs preserving exact `camelCase` + numeric enum HTTP wire contracts.
4. Synchronized `AzureSqlDatabaseInitializer` and `SqliteDatabaseInitializer` incremental schema migrations with the new columns.

## Consequences
- All 3 database providers (`SQLite`, `AzureSql`, `DocumentDb`) share identical bounded query contracts without leaky `IQueryable<T>`.
- Subsequent PRs in the stack can implement bounded DocumentDB/EF queries, CAS updates, TTL indexes, and security hardening cleanly behind ports.

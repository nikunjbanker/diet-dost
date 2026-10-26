<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261026-137: Dual-Database Schema Reconciliation & Error 207 Prevention Standard

- **Status**: Accepted
- **Date**: 2026-10-26
- **Domain**: Architecture / Persistence
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: [#111](https://github.com/nikunjbanker/diet-dost/issues/111)

---

## 1. Context & Problem Statement
During deployment of PR #110 (Cross-Device QR Login & Disaster Recovery Kit) to Azure Container Apps connecting to Azure SQL Serverless, the container app crashed during startup with SQL Server Error 207 (Class 16, State 1):
```
Invalid column name 'IsAnonymous'
at Nutrition.WebGateway.Extensions.DatabaseInitializationExtensions.EnsureSuperAdminAccountAsync in DatabaseInitializationExtensions.cs:line 1270
```
Because the Azure SQL database was previously initialized under older schema versions, the `Users` table already existed (`mealsExist == true`), but `EnsureSqlServerDatabaseMigratedAsync` did not reconcile the newly added `ApplicationUser` columns (`IsAnonymous`, `DisplayName`, `RecoveryLookupId`, `RecoveryKeyHash`, `RecoverySalt`, `RecoveryKeyCreatedAtUtc`) or create the `QrSessions` table and associated indexes.

Consequently, Azure Container Apps failed replica health checks for revision `0000025`, causing the platform to maintain traffic routing on the older healthy revision `0000024`, which served the legacy web application without QR auth.

Furthermore, an existing column index on `Meals.ClientMutationId` threw an index key error in SQL Server because `EnsureColumnExistsAsync` mapped `TEXT` to `NVARCHAR(MAX)` (which SQL Server rejects as an invalid index key).

---

## 2. Decision Drivers
1. **Zero-Error-207 Mandate**: Any entity or column change must work seamlessly when upgrading an existing production database without requiring manual DDL scripts.
2. **Dual-Provider Parity**: Diet-Dost actively targets SQLite (local dev, offline-first mobile sync) and Azure SQL Serverless (cloud deployment).
3. **Automated Dual-Provider Gate**: Integration tests must execute against real SQLite files and SQL Server LocalDB to catch schema divergences before PR creation.

---

## 3. Considered Options
- **Option 1**: EF Core automatic migrations (`dotnet ef migrations add`). Rejected: Requires design-time bundle generation, introduces migration bundle synchronization overhead, and complicates zero-downtime containerized Azure SQL execution.
- **Option 2 (Selected)**: Dynamic idempotent schema reconciliation across both SQLite and SQL Server in `DatabaseInitializationExtensions.cs` backed by automated `DualDatabaseSchemaMigrationTests`.

---

## 4. Decision & Implementation
1. **Explicit Table Mapping**: Configured `QrLoginSession` with `entity.ToTable("QrSessions")` in `DietTrackerDbContext.cs`.
2. **Azure SQL Reconciler**:
   - Added table reconciliation for `QrSessions` with indexes (`ExpiresAtUtc`, `Status`, `ExchangeTicket`).
   - Added column reconciliation for `Users` (`IsAnonymous`, `DisplayName`, `RecoveryLookupId`, `RecoveryKeyHash`, `RecoverySalt`, `RecoveryKeyCreatedAtUtc`, `TermsAcceptedAtUtc`, `TermsVersionAccepted`, `HealthConsentAcceptedAtUtc`, `HealthConsentVersionAccepted`, `ConsentIpAddress`, `ConsentUserAgent`, `LastLoginAtUtc`).
   - Fixed `Meals.ClientMutationId` by enforcing `NVARCHAR(128) NULL` for SQL Server and auto-altering existing `NVARCHAR(MAX)` instances prior to index creation.
3. **SQLite Reconciler**:
   - Added identical table creation for `QrSessions` and column additions for `Users`.
4. **Contract & Integration Tests**:
   - Implemented `DualDatabaseSchemaMigrationTests` validating both SQLite and SQL Server LocalDB migrations against legacy pre-existing schemas.
5. **Governance Codification**: Added Rule 24 in `AGENTS.md` and updated `clean-architecture-cqrs-rules.md`.

---

## 5. Consequences & Forward Roadmap Impact
- **Positive**: 100% automated protection against SQL Error 207 and schema migration failures across cloud deployments.
- **Forward Compatibility**: Prepares Diet-Dost for Phase 3 enterprise cloud database evolution (hybrid Azure SQL + Cosmos DB) with guaranteed backward compatibility for existing user databases.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-192: DocumentDB PascalCase 13-Collection Indexes, TTLs, and DataMigrator Aggregate Parity

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Architecture (`docs/adr/architecture/`)
- **Supersedes / Extends**: ADR-20261126-191

## Context
`DocumentDbDatabaseInitializer` previously created indexes on mismatched lowercase/snake_case collection names (`"users"`, `"meals"`, `"tier_configurations"`) while `DocumentDbRepository<T>` used PascalCase (`"Users"`, `"Meals"`, `"TierConfigurations"`). Ephemeral collections (`VerificationOtps`, `QrSessions`, `AppNotifications`) lacked TTL indexes, and `DataMigrator` had an invalid `Meals` index (`CreatedAtUtc`), an N+1 `CountDocumentsAsync` loop in `SkipExisting`, incomplete enum resolution, and standalone `FoodItems` collection writes in DocumentDB.

## Decision
1. Updated `DocumentDbDatabaseInitializer` to provision compound and unique indexes across all 13 canonical PascalCase collections, plus native TTL indexes on `VerificationOtps.ExpiresAtUtc` (24h), `QrSessions.ExpiresAtUtc` (24h), and `AppNotifications.ReadAtUtc` (90d).
2. Fixed `DocumentDbTargetWriter` `Meals` index (`LoggedAt`), replaced the N+1 `SkipExisting` loop with a batched `$in` `_id` query, and embedded relational `FoodItems` rows directly into `Meals.Items` without creating a standalone `FoodItems` collection.
3. Expanded `DataConversionEngine.ResolveKnownEnum` (`Status`, `OperationType`, `Channel`, `Sex`, `ActivityLevel`, `DietaryPreference`, `DetectionMode`) and synchronized `AzureSqlTargetWriter` DDL with PR 1 columns.

## Consequences
- DocumentDB queries hit PascalCase compound indexes and automatically expire stale OTPs, QR sessions, and read notifications.
- `DataMigrator` preserves the 13-collection embedded `Meals.Items` aggregate model across SQLite, Azure SQL, and DocumentDB.

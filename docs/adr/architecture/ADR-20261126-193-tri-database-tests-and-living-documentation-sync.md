<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-193: Tri-Database Contract Tests, E2E Tier Script Fix, and Living Documentation Sync

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Architecture (`docs/adr/architecture/`)
- **Supersedes / Extends**: ADR-20261126-192

## Context
Following PRs 1–4 (contracts, handlers, DocumentDB CAS/ports, indexes/TTLs/DataMigrator), regression coverage and living documentation needed to be synchronized to prevent drift and verify `validate_e2e_tiers.ps1`.

## Decision
1. Added `DocumentDbCleanArchitectureTests` covering `MarkNotificationReadCommand` IDOR protection, `VerifyOtpCommand` inactive-user rejection, `AdminUpdateUserCommand` SuperAdmin tier escalation guard, fail-closed DocumentDB connection & TLS validation, `DataConversionEngine` enum resolution, DPDPA `DeleteAccountCommand` physical directory erasure, and `InMemoryAiQuotaReservationGate` serialization.
2. Verified and updated `tests/validate_e2e_tiers.ps1` and synchronized `docs/sdd/02_solution_architecture.md` and `.agents/skills/diet-dost-database-architecture/SKILL.md` with the 13-collection DocumentDB NoSQL architecture.

## Consequences
- All DocumentDB, Clean Architecture, and security contracts are covered by deterministic automated tests and living documentation.

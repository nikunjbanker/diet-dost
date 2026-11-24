<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261124-188: Azure SQL Serverless Free Tier Transition & Schema Reconciliation

> **Date / Timestamp**: 2026-11-24T13:25:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Diet-Dost Engineering  
> **Change Type**: ARCHITECTURE_REFACTOR  
> **Affected Subsystems**: Infrastructure, CI/CD, Database, Tools  
> **Associated PR & Stack**: Dedicated Feature PR  
> **Relevant SDDs & CFTs**: [`docs/sdd/05_devops_and_infrastructure.md`](../../sdd/05_devops_and_infrastructure.md), [`docs/sql-setup-guide.md`](../../sql-setup-guide.md)

---

## 1. Executive Summary & Change Rationale

With **Azure DocumentDB** now established as Diet-Dost's primary cloud persistence provider (ADR-177, ADR-187), retaining Azure SQL on the paid provisioned Basic DTU tier incurs continuous infrastructure charges for an idle database. This ADR transitions Azure SQL to the **General Purpose Serverless Free Tier** (`GP_S_Gen5_1`, 1 vCore max, 0.5 vCore min, 32 GB storage limit, 60m auto-pause delay) with the official lifetime Free Offer (`useFreeLimit: true`, `freeLimitExhaustionBehavior: AutoPause`), reducing compute charges to **$0.00** when idle.

Because Azure prohibits updating an existing paid database to the free tier offer in place (`Cannot update paid database to free database`), the development database was re-created with the free offer applied, and all 582 domain records were synchronized with 100% parity from DocumentDB using `Nutrition.Tools.DataMigrator`. Additionally, missing schema definitions for `QrSessions` and `AppNotifications` were implemented in `AzureSqlTargetWriter.cs`.

## 2. Context and Problem Statement

1. **Cost Optimization**: Azure SQL Basic DTU was previously pinned via ADR-160 to avoid unmetered vCore bills while SQL Server was the primary database. Now that DocumentDB is primary, an auto-pausing serverless free tier is the optimal secondary cloud relational store.
2. **Platform In-Place Constraint**: Azure Resource Manager does not permit `useFreeLimit: true` to be enabled on an existing database originally created as Basic DTU.
3. **DataMigrator Table Coverage**: During reverse synchronization from DocumentDB to Azure SQL, `AzureSqlTargetWriter` lacked DDL definitions in `EnsureSchemaForTableAsync` for `QrSessions` and `AppNotifications`.

## 3. Decision Drivers

- Reduce Azure SQL compute costs to $0.00 when inactive via serverless auto-pause.
- Leverage Azure's lifetime SQL Free Offer (100,000 vCore-seconds/month and 32 GB storage).
- Maintain 100% multi-database parity across DocumentDB and Azure SQL.
- Harmonize Bicep templates (`sql.bicep`, `infra.bicep`, `main.bicep`), deployment workflows, and automated tests.

## 4. Considered Options

- **Option 1: Retain Basic DTU**. Rejected due to recurring monthly provisioned cost for a secondary database.
- **Option 2: Serverless without Free Offer (`useFreeLimit: false`)**. Pauses compute, but does not grant the free 100,000 vCore-seconds or 32 GB free storage.
- **Option 3: Re-create Database with Free Offer & Full DocumentDB Sync**. Chosen. Re-provisions with `useFreeLimit: true` and restores 100% of data from DocumentDB with zero data loss.

## 5. Decision Outcome

- **Chosen Option**: General Purpose Serverless Free Tier (`GP_S_Gen5_1`, 32 GB storage ceiling, 60-minute auto-pause delay, `AutoPause` exhaustion behavior).
- **IaC Codification**: `infra/dev/sql.bicep` provisions `GP_S_Gen5_1` with `useFreeLimit: useFreeLimit` and `freeLimitExhaustionBehavior: freeLimitExhaustionBehavior`.
- **Tooling Fix**: Added `QrSessions` and `AppNotifications` DDL to `AzureSqlTargetWriter.cs`.
- **Automated Regression Guard**: Updated `SqlBicep_ProvisionsServerlessFreeTier_AndConfiguresAutoPause` in `AdversarialAndSecurityRemediationTests.cs`.

## 6. Consequences & Trade-Offs

### Positive Consequences
- **Zero Idle Compute Cost**: Database enters Paused state after 60 minutes of inactivity.
- **Free Monthly Quota**: 100,000 vCore-seconds and 32 GB storage covered under Azure SQL Free Offer.
- **Complete Parity**: All 14 domain collections (582 records) successfully synchronized into Azure SQL.

### Negative Consequences / Accepted Trade-Offs
- **Cold Boot Latency**: If Azure SQL is queried while paused, a 15-45 second wake-up delay occurs before the first query executes. The application's 60s command timeout and EF Core transient retry policies gracefully handle this.

## 7. Verification & Compliance Results

- **Data Migration**: 582 records across 14 tables verified with 0 errors in report `migration_report_20261124_074723.md`.
- **Live Azure Verification**: `useFreeLimit: true`, `kind: "v12.0,user,vcore,serverless,freelimit"`, `status: Online`, tags intact.
- **Bicep Compilation**: All templates validated with zero errors (`az bicep build`).

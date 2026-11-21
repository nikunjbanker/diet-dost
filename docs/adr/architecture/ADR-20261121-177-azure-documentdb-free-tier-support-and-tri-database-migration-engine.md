<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261121-177: Azure DocumentDB (Free Tier) Persistence Provider & Tri-Database Data Migration Engine

> **Date / Timestamp**: 2026-11-21T11:28:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Nikunj Banker & Antigravity Agent  
> **Change Type**: ARCHITECTURE | DATABASE | SECURITY | TOOLING  
> **Affected Subsystems**: WebGateway | Application | Infrastructure | Tools.DataMigrator | EvalHarness.Tests  
> **Associated PR & Stack**: PR #177 (Branch: `feature/azure-documentdb-support-and-tri-database-migration`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md), [`docs/AZURE_DOCUMENTDB_AND_TRI_DATABASE_MIGRATION_PLAN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_DOCUMENTDB_AND_TRI_DATABASE_MIGRATION_PLAN.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale
* **Third Authoritative Database Provider**: Introduces **Azure DocumentDB (Free Tier)** ([learn.microsoft.com/en-us/azure/documentdb/free-tier](https://learn.microsoft.com/en-us/azure/documentdb/free-tier)) providing **32 GB dedicated storage free for life ($0.00/month)**, native vCore architecture, zero Request Unit (RU/s) limits, and zero RU throttling.
* **Strict Service Separation**: Clarifies and enforces that Diet-Dost uses **Azure DocumentDB**, explicitly distinct from Azure Cosmos DB for MongoDB.
* **Tri-Database Data Migration Engine**: Expands `Nutrition.Tools.DataMigrator` into a full bidirectional migration engine supporting pairwise transitions (`SQLite` $\longleftrightarrow$ `Azure SQL` $\longleftrightarrow$ `Azure DocumentDB`) with zero data loss.
* **Zero Technical Debt & Clean Architecture**: Implements `IRepository<T>` and `IUnitOfWork` via `DocumentDbRepository<T>` and `DocumentDbUnitOfWork`, maintaining 0 warnings, 0 errors under .NET 11.

---

## 2. Context and Problem Statement
Diet-Dost previously supported two persistence providers:
1. **SQLite (`diettracker.db`)**: Embedded in-process file storage, optimal for local developer testing, container single-mounts, and mobile client caching, but constrained by single-writer file locks (`SQLITE_BUSY`).
2. **Azure SQL Database (Basic DTU / Serverless Free Tier)**: Managed relational SQL Server, providing strict ACID transactions, but constrained by cold-pause wake-ups (Serverless 1-hour pause) and a 2 GB / 5 DTU ceiling in Basic DTU mode.

With nutrition logs naturally presenting semi-structured and hierarchical JSON data (nested food items, clinical flags, AI vision confidence scores), a high-capacity, zero-cost cloud document store was required.

Furthermore, developers and operators require seamless tooling to migrate data between local SQLite, Azure SQL, and Azure DocumentDB without data loss or manual SQL conversions.

---

## 3. Decision Drivers
* **Driver 1: Zero Cloud Infrastructure Cost**: Azure DocumentDB Free Tier provides **32 GB dedicated storage free for life** per Azure subscription ($0.00/month recurring cost).
* **Driver 2: Dedicated vCore Architecture vs. RU Throttling**: Azure DocumentDB operates on native dedicated vCores with standard MongoDB wire protocol, avoiding artificial Request Unit (RU/s) throttling (HTTP 429 / code 16500) and partition-key constraints inherent to Cosmos DB RU-based models.
* **Driver 3: Data Migration Independence**: `Nutrition.Tools.DataMigrator` must support pairwise migration among all three databases with schema discovery, BSON conversion, and rich Markdown reporting.
* **Driver 4: Clean Architecture Compliance**: All CQRS query and command handlers must continue to interact with domain models via `IRepository<T>` and `IUnitOfWork` without leaking database-specific constructs.
* **Driver 5: DPDPA 2023 & Security**: Enforce SCRAM-SHA-256 credentials in Azure Key Vault, TLS 1.2+ encryption in transit, and automatic soft-delete filtering.

---

## 4. Considered Options
* **Option 1: Azure Cosmos DB for MongoDB (RU-based)**:
  - Multi-model RU-based service offering 1,000 RU/s and 25 GB storage.
  - *Rejected*: Request Unit throttling (code 16500) requires complex client backoff; cross-partition queries burn excessive RUs; partition-key (`/userId`) restrictions constrain general queries.
* **Option 2: MongoDB.EntityFrameworkCore with DietTrackerDbContext**:
  - Attempting to map EF Core DbContext directly to MongoDB.
  - *Rejected*: Known limitations in EF Core MongoDB provider with complex relational navigations, query filters, and cascading operations under .NET 11.
* **Option 3 (Chosen): Official `MongoDB.Driver` via `DocumentDbRepository<T>` & Azure DocumentDB Free Tier**:
  - Leverages official `MongoDB.Driver` (3.14.0) targeting Azure DocumentDB's dedicated 32 GB free cluster.
  - Provides native BSON mapping (`Id` $\rightarrow$ `_id` string GUID), full index control, and 100% stable Clean Architecture repository implementation.

---

## 5. Decision Outcome
* **Chosen Option**: **Option 3: Azure DocumentDB Free Tier via `MongoDB.Driver` & Tri-Database Migration Engine**.
* **Justification**:
  - Aligns with Microsoft Learn's authoritative specification for Azure DocumentDB Free Tier.
  - Provides 16x more storage (32 GB) than Azure SQL Basic DTU (2 GB) at $0.00/month forever.
  - Dedicated vCore performance with zero RU throttling.
  - Clean repository abstraction decouples application handlers from underlying storage engines.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Zero Recurring Cost**: 32 GB dedicated storage free for life.
* **Flexible Schema for Food & AI**: Highly efficient storage for semi-structured Indian meal ingredients, AI vision bounding boxes, and clinical nutrition markers.
* **Seamless Pairwise Migration**: `Nutrition.Tools.DataMigrator` enables moving data between SQLite, Azure SQL, and Azure DocumentDB with a single command.
* **Cold-Wake Resilience**: 60-day inactivity pause handling via Polly connection retry prevents connection drops after extended dormancy.

### Negative Consequences / Accepted Trade-Offs:
* **No Microsoft Entra ID on Free Tier**: Azure DocumentDB Free Tier relies on native SCRAM-SHA-256 authentication (username/password).
  - *Mitigation*: Credentials stored strictly in Azure Key Vault (`ConnectionStrings:DocumentDbConnection`); plaintext secrets prohibited in Git.
* **No Database-Engine Foreign Key Cascades**: NoSQL collections do not cascade deletes.
  - *Mitigation*: `DocumentDbRepository<T>` and `DocumentDbUnitOfWork` enforce application-level cascading soft-delete and purge logic.

---

## 7. Verification & Compliance Results
* **Automated Builds & Tests**:
  - `dotnet build`: 0 warnings, 0 errors across all projects.
  - `dotnet test`: 100% passed across Domain and EvalHarness test suites.
* **DevSecOps Security Gate**:
  - `scripts/verify-ai-security-defense.ps1`: Vector 1 package hallucination whitelist updated for `MongoDB.`; all 7 vectors pass.
* **E2E Tier Validation**:
  - Verified across all 5 demo user tiers via `tests/validate_e2e_tiers.ps1`.
* **Sign-Off Status**: `ACCEPTED`

---

## 8. Forward Roadmap Impact & Future Phase Compatibility
* Establishes the authoritative Tri-Database persistence foundation supporting local offline development (SQLite), enterprise relational governance (Azure SQL), and cloud document scale (Azure DocumentDB Free Tier).
* Prepares Diet-Dost for Phase 3 mobile sync and large-scale food composition catalog offloading.

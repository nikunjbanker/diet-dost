<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261121-179: Azure DocumentDB Free Tier Bicep IaC, CI/CD Pipeline Automation, and Database Initialization OOP Refactoring

- **Status**: Accepted
- **Date**: 2026-11-21
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud DevOps Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #182
- **Related Issue**: #181

---

## 1. Context & Problem Statement

Adversarial review of the Diet-Dost database persistence architecture identified that while application-level support for Azure DocumentDB (Free Tier) and tri-database migration was implemented, corresponding cloud infrastructure automation and deployment pipelines were incomplete:
1. **Missing IaC Provisioning Module**: No Bicep module existed to provision Azure DocumentDB (Free Tier - 32 GB dedicated storage) on demand.
2. **Missing CI/CD Workflow Integration**: Neither `.github/workflows/azure-infra-deploy.yml` nor `.github/workflows/azure-app-deploy.yml` contained input parameters, compilation validation, or secret wiring for the DocumentDB persistence provider.
3. **Monolithic DatabaseInitializationExtensions Violation**: `DatabaseInitializationExtensions.cs` had grown into a >2,300-line static class containing duplicate data seeding logic across EF Core and MongoDB drivers, violating Single Responsibility (SRP), Open/Closed (OCP), and Don't Repeat Yourself (DRY) principles.

---

## 2. Decision & Implementation

### 2.1 Azure DocumentDB (Free Tier) Bicep IaC Module (`infra/dev/documentdb.bicep`)
- **Resource Declaration**: Created `Microsoft.DocumentDB/mongoClusters@2024-07-01` configured with `compute.tier = 'Free'`, `serverVersion = '6.0'`, and `storage.sizeGb = 32`.
- **Architectural Guardrail**: Strictly targets **Azure DocumentDB (Free Tier)** with dedicated vCore architecture, zero Request Unit (RU) limits, and zero RU throttling, explicitly distinguishing it from Azure Cosmos DB for MongoDB. Reference: `https://learn.microsoft.com/en-us/azure/documentdb/free-tier`.
- **Network Perimeter**: Added delegated subnet NSG outbound rule `Allow-DocumentDB-Outbound` allowing destination ports `10255` and `27017` to destination service tag `AzureCosmosDB`.
- **Key Vault Sole Authority**: Admin password and constructed connection string are stored directly into Key Vault as secret `documentdb-connection-string`. Zero plaintext connection strings are emitted in Bicep outputs.

### 2.2 CI/CD Pipeline Integration (`.github/workflows/`)
- **Infrastructure Pipeline (`azure-infra-deploy.yml`)**: Added inputs `deployDocumentDb` and `documentDbAdminLogin`, Stage 1 compilation verification (`az bicep build --file infra/${{ inputs.environment }}/documentdb.bicep`), ARM validate / what-if / deploy parameter wiring, and Stage 3 endpoint reporting.
- **Application Pipeline (`azure-app-deploy.yml`)**: Added `DocumentDb` option to `databaseProvider`, automated foundation infrastructure provisioning, and wired Key Vault secret reference `ConnectionStrings__DocumentDbConnection` into the Container App container definition.

### 2.3 OOP / SOLID Refactoring of Database Initialization
- **Strategy Pattern (`IDatabaseInitializer`)**: Decoupled provider-specific schema tasks into:
  - `SqliteDatabaseInitializer`: PRAGMA setup, SMB lock resilience, fast `/tmp` bootstrapping, schema migrations.
  - `AzureSqlDatabaseInitializer`: DDL execution, table reconciliation, and 42119 quota pause resilience.
  - `DocumentDbDatabaseInitializer`: Ping warmup with pause/auto-resume resilience, collection creation, and unique sparse indexes.
- **Factory Pattern (`IDatabaseInitializerFactory`)**: `DatabaseInitializerFactory` dynamically resolves the correct strategy based on `DatabaseOptions.Provider`.
- **Ordered Seeders (`IDatabaseSeeder`)**: Decoupled data seeding into ordered components:
  - `AppSecretsSeeder` (Order 10): Cryptographic key seeding for development environments.
  - `DefaultDataSeeder` (Order 20): Unified seeder operating over `IRepository<T>` and `IUnitOfWork`, eliminating duplicated seeding logic between EF Core and MongoDB.
  - `StorageMediaMirrorSeeder` (Order 30): Static seed media mirroring to persistent volume mounts.
- **Lean Facade (`DatabaseInitializationExtensions.cs`)**: Reduced by 95% into a backward-compatible facade delegating directly to `DatabaseInitializationService`.

---

## 3. Consequences & Governance

- **100% IaC Coverage**: Azure DocumentDB can be provisioned, previewed, and validated declaratively through Bicep alongside existing SQLite and Azure SQL infrastructure.
- **SOLID Compliance**: Zero duplicate seeding code. Adding a new database provider requires only implementing `IDatabaseInitializer` without modifying existing schema initializers or seeders.
- **Automated Verification**: Verified across 13 Bicep contract tests, 5 factory unit tests, 6 migration/seeding tests, and 522 total solution tests passing with 0 warnings.

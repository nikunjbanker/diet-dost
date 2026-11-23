<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261123-185: Azure DocumentDB Default Cloud Persistence Provider & Automated Workflow Harmonization

> **Date / Timestamp**: 2026-11-23T16:05:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Nikunj Banker & Antigravity Agent  
> **Change Type**: DEVOPS | DATABASE | CI/CD  
> **Affected Subsystems**: Workflows | Infrastructure | WebGateway  
> **Associated PR & Stack**: PR #188 (Branch: `feature/documentdb-default-cloud-provider`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md), [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md)  

---

## 1. Executive Summary & Change Rationale
* **Default Persistence Provider Harmonization**: Configures **Azure DocumentDB (Free Tier - 32 GB dedicated storage)** as the default cloud persistence provider across `.github/workflows/auto-release-on-merge.yml` and `.github/workflows/azure-app-deploy.yml`.
* **Zero Hardcoded Provider Overrides**: Eliminates hardcoded `-f databaseProvider="AzureSql"` in `auto-release-on-merge.yml`, adding dynamic `databaseProvider` workflow dispatch inputs and cascading values into `azure-app-deploy.yml`.
* **Zero-Cost High-Capacity Production Workloads**: Adopts Azure DocumentDB Free Tier as primary to eliminate Azure SQL Serverless auto-pause cold-starts and 2 GB capacity bottlenecks while preserving 100% data fidelity.

---

## 2. Context and Problem Statement
Following the successful provisioning of Azure DocumentDB cluster `docdb-dietdost-dev-skehxuagttzos` in `rg-dietdost-dev`, deployments triggered via the `auto-release-on-merge.yml` lifecycle daemon continued selecting `AzureSql` because line 196 explicitly passed `-f databaseProvider="AzureSql"`.
This caused the application to continue running against Azure SQL rather than utilizing the lifetime Free Tier DocumentDB instance.

---

## 3. Decision Drivers
1. **Developer Expectation**: Merges to `main` must deploy using the targeted cloud database (Azure DocumentDB) without requiring manual parameter overrides.
2. **Workflow Consistency**: Both automated release daemon and on-demand dispatch workflows must share the same default database provider.
3. **Tri-Database Portability**: Retain full backwards-compatibility for on-demand SQLite and Azure SQL deployments.

---

## 4. Verification Evidence
- Verified `auto-release-on-merge.yml` passes `$dbProvider` dynamically to `azure-app-deploy.yml`.
- Verified `azure-app-deploy.yml` defaults to `DocumentDb`.
- Actionlint and AI Security Defense gates passed.

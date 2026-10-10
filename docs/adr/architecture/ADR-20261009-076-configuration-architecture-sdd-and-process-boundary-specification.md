/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261009-076: Codification of Configuration, Options Pattern & Secret Management Architecture SDD

- **Status**: Accepted
- **Date**: 2026-10-09
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal DevOps Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and Security Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
With the implementation of the .NET Aspire AppHost orchestrator, centralized configuration provider (`AddDietDostAppConfiguration`), Azure Key Vault secret governance, and strongly-typed Options with FluentValidation, team members and future AI agents required an authoritative, living system design document (SDD) explaining:
1. Why Aspire AppHost needs process-level environment variable bridging (`Environment.GetEnvironmentVariable` and `.WithEnvironment(...)`).
2. When each of the 4 configuration mechanisms (`DatabaseSecretStore`, `Azure Key Vault`, `IConfiguration` json files, and `Environment Variables`) is utilized in Local/Debug vs. Deployed/Release environments.
3. The architectural distinction between `Nutrition.AppHost/Program.cs` and `Nutrition.WebGateway/Program.cs`.
4. A permanent maintenance protocol ensuring the specification remains updated as configuration models evolve.

---

## 2. Decision Drivers
- **Living Proof & Single Source of Truth**: Establish [`docs/sdd/10_configuration_and_secret_management_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/10_configuration_and_secret_management_architecture.md) as the authoritative reference.
- **Process Model Clarity**: Codify the 2-tier process boundary between Aspire orchestrator and application workloads.
- **Zero Secrets in Git**: Maintain strict zero-secrets-in-git compliance with offline local developer capability and cloud Key Vault parity.
- **Zero Documentation Drift**: Guarantee clear guidelines for synchronizing the SDD whenever new options, secrets, or deployment pipelines are added.

---

## 3. Decision Outcome
**Chosen Decision**: Approved and committed [`docs/sdd/10_configuration_and_secret_management_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/10_configuration_and_secret_management_architecture.md) and registered it across the SDD Master Index (`00_sdd_index.md`) and Living Documentation Log (`07_living_documentation_log.md`).

### Key Codifications:
1. **2-Tier Process Model**: Documented the isolation between AppHost OS process memory and WebGateway subprocess launch table.
2. **4 Configuration Mechanisms Hierarchy**:
   - *Local Dev*: `appsettings.Development.json` + local SQLite `AppSecrets` (0 cloud cost, 100% offline).
   - *Production ACA*: `Azure Key Vault` via Managed Identity + Bicep environment variables (0 secrets on disk/git).
3. **AppHost vs. WebGateway Roles**:
   - *AppHost*: Declarative infrastructure modeler & subprocess launcher; zero domain/EF Core knowledge.
   - *WebGateway*: Application workload server; owns DI, middleware, EF Core, and business logic.
4. **Living Update Protocol**: Formalized rules for adding new options classes, altering cloud mappings, and updating validators.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile BFF**: Mobile client configuration contracts inherit the established centralized configuration architecture.
- **Phase 3 Cloud Database**: Azure SQL / Cosmos DB connection strings will plug directly into `DatabaseOptions` without breaking consumers.
- **Agentic Reusability**: AI coding agents have a definitive, single-source reference for resolving configuration ambiguities.

---

## 5. Verification & Validation Evidence
- **Document Created**: [`docs/sdd/10_configuration_and_secret_management_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/10_configuration_and_secret_management_architecture.md).
- **Index Registered**: Registered in [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md).
- **ADR Registered**: Registered in [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md) and [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md).
- **Test Suite Status**: 209 / 209 tests passing (0 warnings, 0 errors).

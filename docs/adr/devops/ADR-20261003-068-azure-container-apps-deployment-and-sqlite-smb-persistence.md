<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-068: Azure Container Apps Deployment with Persistent SQLite SMB Volume Mount, Private VNet Service Endpoints, and Showcase Demo Governance

* **Status**: Accepted
* **Date**: 2026-10-03
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `azure-container-apps`, `sqlite-persistence`, `smb-mount`, `podman`, `bicep-iac`, `dev-diet-dost-in`, `security-allow-demo-users`, `forward-roadmap`

---

## 1. Context & Problem Statement

As codified in [docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) (v2.4.0) and [docs/adr/ADR-20260929-051-mvp-market-roadmap-reprioritization.md](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260929-051-mvp-market-roadmap-reprioritization.md), **Phase 1 Layer 9 (Issue #31)** marks the culmination of **Milestone: Alpha Release 01 (Workable Web Showcase MVP on Azure)**.

Following the successful implementation of responsive UI ergonomics (#40), Mobile BFF contracts (#41), and Admin read-only tier governance (#52), the application required cloud deployment to Azure Container Apps (ACA) under `https://dev.diet-dost.in`. Deploying an embedded SQLite database in cloud container environments introduces three fundamental engineering challenges:

1. **Ephemeral Container Storage vs. SQLite Data Persistence**:
   Containers are ephemeral; restarting or updating an image revision destroys local container file storage. Azure Blob Storage does not provide POSIX random-access byte-range file locking (`fcntl`/`flock`), leading to immediate database corruption or data loss on container termination.
2. **Multi-Replica SQLite Concurrency & Lock Deadlocks**:
   SQLite is a single-writer embedded database. Scaling ACA to multiple replicas over a shared network mount causes instant locking deadlocks and database corruption.
3. **Private Access & Network Perimeter Isolation**:
   The cloud storage holding user data and meal uploads must not be exposed to the public internet. Access must be private to the Azure Container Apps environment with proper role-based or network access control.
4. **Demo Account Gating in Release Mode vs. Showcase Showcase Requirements**:
   `IAppEnvironment` originally restricted demo user seeding and authentication strictly to `IsDebugMode && IsDevelopment`. Compiling production containers with `-c Release` causes demo accounts to be suppressed and deactivated, directly conflicting with the requirement that the public showcase environment (`dev.diet-dost.in`) provide functional seeded demo accounts across all 5 user tiers.

---

## 2. Decision Drivers

1. **Zero Data Loss & Strict SQLite Persistence**:
   Ensure zero data loss across container restarts, crash recoveries, and deployments by mounting a durable Azure Files SMB 3.0 share to `/app/data`.
2. **Single-Replica Invariant (`minReplicas: 1`, `maxReplicas: 1`)**:
   Enforce the single-replica constraint in Infrastructure as Code to preserve ACID transactional integrity and eliminate SMB lock contention.
3. **Private Storage Perimeter**:
   Isolate Azure Files SMB share behind a dedicated Virtual Network with delegated ACA subnet (`Microsoft.App/environments`), service endpoint (`Microsoft.Storage`), and storage network ACL `defaultAction: Deny`.
4. **Podman Containerization Standard**:
   Package the application using rootless, daemonless OCI container tooling (`Podman` and `Containerfile`), adhering to repository standards.
5. **Showcase Demo Governance via Explicit Configuration**:
   Provide an explicit configuration toggle `Security:AllowDemoUsers` (`Security__AllowDemoUsers=true`) allowing the `dev.diet-dost.in` showcase to activate demo accounts in Release builds without weakening production security defaults.
6. **Zero Cost / Ultra-Low Footprint**:
   Operate entirely within the ACA free grant tier (180,000 vCPU-seconds, 360,000 GiB-seconds, 2M requests/month free) and Azure Files Standard LRS (< $0.30/month).

---

## 3. Considered Options

* **Option 1: Azure App Service (F1 Free / B1 Basic)**:
  - *Critique*: F1 lacks AlwaysOn (app sleeps after 20 minutes) and does not support free managed SSL certificates. B1 costs ~$13/month.
* **Option 2: Azure Blob Storage with Startup Sync**:
  - *Critique*: Catastrophic data loss risk. Any ungraceful container kill or scale event loses all writes since the previous sync.
* **Option 3 (Selected): Azure Container Apps + Azure Files SMB Volume Mount + Private VNet Service Endpoints**:
  - Deploy to ACA with persistent SMB mount at `/app/data`.
  - Fix replicas to `minReplicas: 1, maxReplicas: 1`.
  - Secure storage access via VNet delegated subnet and `Microsoft.Storage` service endpoint.
  - Bind custom domain `dev.diet-dost.in` with free auto-renewing Azure-managed TLS 1.3 certificate.

---

## 4. Decision Outcome & Architecture

1. **Podman OCI Packaging (`Containerfile`)**:
   - Multi-stage Linux build targeting `.NET 11` SDK and ASP.NET Core runtime (`mcr.microsoft.com/dotnet/aspnet:11.0-preview`).
   - Prepared durable mount point `/app/data` with full write permissions.
   - Configured environment variables:
     - `Database__Provider=Sqlite`
     - `ConnectionStrings__DefaultConnection=Data Source=/app/data/diet_dost.db;Cache=Shared`
     - `Storage__WebRootPath=/app/data/wwwroot`
     - `Security__AllowDemoUsers=false` (default safe)
2. **Infrastructure as Code (`infra/main.bicep` & `infra/main.parameters.json`)**:
   - Virtual Network with delegated subnet `snet-aca-infra` (`10.0.0.0/23`) for `Microsoft.App/environments`.
   - Azure Storage Account with Standard LRS SMB share (`dietdost-data`) and network ACLs denying public traffic and permitting only the VNet subnet.
   - ACA Managed Environment linked to Log Analytics Workspace and storage mount `dietdoststorage`.
   - Container App `app-dietdost-web` with external ingress on port 8080, volume mount `/app/data`, and single-replica enforcement.
3. **On-Demand GitHub Actions Deployment (`.github/workflows/azure-deploy.yml`)**:
   - Single authoritative deployment vehicle triggered on-demand via `workflow_dispatch` with parameterization (`allowDemoUsers`).
   - Executes unit/domain test suites, builds and pushes OCI container image with Podman, provisions Bicep infrastructure, and asserts health.
   - Eliminates redundant local script sprawl by consolidating all deployment automation in CI/CD.
5. **Showcase Demo User Governance**:
   - Extended `IAppEnvironment` and `AppEnvironment` to check `_configuration.GetValue<bool>("Security:AllowDemoUsers") || (IsDebugMode && IsDevelopment)`.
   - Allows `dev.diet-dost.in` showcase deployment to activate demo accounts (`free`, `basic`, `premium`, `admin.demo`, `superadmin`) in Release mode while ensuring production remains strictly secured.
6. **Living Documentation & Acceptance**:
   - Created CFT acceptance document [`docs/cft/cft_azure_cloud_deployment_aca.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_azure_cloud_deployment_aca.md).

---

## 5. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Native Mobile Integration**:
  Provides the live HTTPS public endpoint (`https://dev.diet-dost.in/api/mobile/v1/*`) that .NET MAUI Android and iOS apps connect to directly for authentication, dashboard composite hydration, and offline sync verification.
* **Phase 3 Enterprise Cloud Database Migration**:
  The infrastructure cleanly decouples persistence via `Database__Provider` and connection strings. When transitioning from SQLite to Azure SQL Serverless Free Tier, only configuration variables change; the ACA compute container, VNet integration, and custom domain TLS infrastructure remain 100% reusable.

---

## 6. Verification Status

* **Unit & Domain Tests**: 164 / 164 tests passed (128 EvalHarness + 36 Domain).
* **Podman Packaging**: Verified multi-stage build using Podman OCI engine.
* **Infrastructure Validation**: Bicep template syntax and resource dependency graph validated.
* **Living Documentation Log**: Registered as ADR-068 in `docs/adr/README.md` and `docs/sdd/07_living_documentation_log.md`.

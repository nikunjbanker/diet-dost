<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261008-070: .NET Aspire-Native Cloud Deployment Pipeline and Two-Stage Infrastructure vs. Application Workflow

* **Status**: Accepted
* **Date**: 2026-10-08
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `dotnet-aspire`, `aspire-publish`, `azure-container-apps`, `bicep-iac`, `two-pipeline-architecture`, `app-lifecycle`, `sqlite-persistence`, `forward-roadmap`

---

## 1. Context & Problem Statement

Initial cloud deployment automation for **Phase 1 Layer 9 (Issue #31)** implemented an ad-hoc, monolithic GitHub Actions workflow using raw Azure CLI commands and static Bicep templates. While functionally operational, this bypassed the **.NET Aspire deployment pipeline architecture** ([aspire.dev/deployment/deploy-with-aspire/](https://aspire.dev/deployment/deploy-with-aspire/)):

1. **AppHost Disconnect**: `src/Nutrition.AppHost` was treated purely as an inner-loop local development runner, rather than the authoritative declaration of compute, storage, and deployment targets.
2. **Missing Aspire App Lifecycle**: The four formal lifecycle phases (Inner-loop development $\rightarrow$ Containerized validation $\rightarrow$ CI release publishing $\rightarrow$ Runtime deployment) were not codified.
3. **Monolithic Deployment Risk**: Application code updates unnecessarily re-evaluated foundational cloud infrastructure (VNets, storage accounts, log workspaces), increasing pipeline duration and blast radius.

---

## 2. Decision Drivers

1. **Aspire Application Model as Single Source of Truth**:
   Declare deployment targets (`builder.AddAzureContainerAppEnvironment("cae-dietdost")`) and persistent cloud storage (`builder.AddAzureStorage("dietdost-storage")`) directly within the AppHost application model.
2. **First-Class Aspire CLI Pipeline Steps**:
   Use `aspire publish` to evaluate the AppHost model, resolve parameters, and emit deterministic Bicep infrastructure-as-code manifests.
3. **Two-Stage Enterprise Pipeline Separation**:
   De-couple foundation infrastructure provisioning from application workload deployments to support independent lifecycle velocity and reduced operational blast radius:
   - **Pipeline 1 (`azure-infra-deploy.yml`)**: Provisions / updates foundational cloud infrastructure (VNet, Storage Account, Azure Files SMB share, Log Analytics, ACA Environment, durable storage link).
   - **Pipeline 2 (`azure-app-deploy.yml`)**: Validates security gates (OWASP dependency audit, full test suite), builds OCI images, executes Trivy vulnerability scans, pushes to ACR, and deploys application revisions with persistent SMB mounts.
4. **Preservation of SQLite Zero-Data-Loss Invariants**:
   Strictly preserve the single-replica constraint (`minReplicas: 1, maxReplicas: 1`) and durable SMB volume mount (`/app/data`) across all Aspire-generated Bicep configurations.

---

## 3. Decision Outcome & Architecture

1. **AppHost Packaging & Azure Integration**:
   - Added `Aspire.Hosting.Azure.AppContainers` and `Aspire.Hosting.Azure.Storage` to `src/Nutrition.AppHost/Nutrition.AppHost.csproj`.
   - Updated `Program.cs` and `WebGatewayResourceExtensions.cs` to declare the Azure Container Apps environment and enforce single-replica constraints via `PublishAsAzureContainerApp`:
     ```csharp
     webGateway.PublishAsAzureContainerApp((infrastructure, containerApp) =>
     {
         containerApp.Template.Scale.MinReplicas = 1;
         containerApp.Template.Scale.MaxReplicas = 1;
     });
     ```
2. **Modular Bicep Topology (`infra/`)**:
   - [`infra/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/infra.bicep): Foundation module provisioning VNet, Private Storage Firewall, Azure Files SMB share (`dietdost-data`), Log Analytics Workspace, ACA Environment, and the Durable Storage Mount link (`dietdoststorage`).
   - [`infra/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/app.bicep): Workload module deploying the `app-dietdost-web` Container App, volume mount `/app/data`, single-replica scale constraint, and custom domain `dev.diet-dost.in` TLS binding.
   - [`infra/main.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/main.bicep): Clean composite module orchestrating both layers for full end-to-end deployments.
3. **Dedicated Two-Stage CI/CD Automation & Automated Infrastructure Provisioning**:
   - [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml): Standalone on-demand Aspire infrastructure provisioning pipeline for infrastructure-only operations (VNet, Storage quota, ACA environment reconfiguration).
   - [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml): On-demand Aspire application build and deployment pipeline. Critically, it includes an integrated infrastructure provisioning phase (`provisionInfra: true` by default) that ensures the foundation cloud infrastructure (`infra/infra.bicep`) exists and is current before deploying the application container (`infra/app.bicep`), guaranteeing zero manual orchestration dependencies.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Native Mobile Integration**:
  The Aspire-orchestrated WebGateway hosts the live Mobile BFF (`/api/mobile/v1/*`), providing a robust HTTPS target with deterministic managed identity and telemetry.
* **Phase 3 Enterprise Cloud Database Migration**:
  Transitioning persistence from SQLite to Azure SQL Serverless will occur seamlessly inside `Nutrition.AppHost` by introducing `builder.AddAzureSqlServer(...)`, without disrupting the ACA compute or networking perimeter.

---

## 5. Verification Status

* **Unit & Security Tests**: 188 / 188 passing (36 Domain + 152 EvalHarness) with 0 warnings and 0 errors.
* **Aspire Publish Execution**: 11 / 11 pipeline steps verified successfully via `aspire publish`.
* **Bicep Compilation**: Clean compilation across `infra/infra.bicep`, `infra/app.bicep`, and `infra/main.bicep`.

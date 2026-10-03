<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# CFT Document: Azure Container Apps & Persistent SQLite SMB Deployment Verification

> **Classification**: Customer & Functional Acceptance Test (CFT) Specification  
> **Target Subsystem**: Azure Container Apps (`cae-dietdost-dev`), Persistent Azure Files SMB Share (`dietdost-data`), Custom Domain (`https://dev.diet-dost.in`)  
> **Related SDD**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md)  
> **Related Skill**: [`.agents/skills/diet-dost-azure-deployment/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md)  
> **Milestone**: Alpha Release 01 (Workable Web Showcase MVP on Azure)  

---

## 1. Pre-Flight Infrastructure & Container Verification
- [ ] **Step 1.1**: Container image built successfully with Podman (`podman build -t <acr>.azurecr.io/diet-dost-web:latest -f Containerfile .`).
- [ ] **Step 1.2**: Azure Bicep deployment validated (`az deployment group validate --resource-group rg-dietdost-dev --template-file infra/main.bicep`).
- [ ] **Step 1.3**: Azure Files SMB share provisioned with private VNet service endpoint rule (`Microsoft.Storage`) and default network ACL set to `Deny`.
- [ ] **Step 1.4**: Container App revision active with `minReplicas: 1` and `maxReplicas: 1` (strict SQLite single-replica locking constraint).
- [ ] **Step 1.5**: Persistent volume mounted at `/app/data` pointing to Azure File share `dietdost-data`.

---

## 2. Test Suite 1: Live Cloud Connectivity & Custom Domain TLS 1.3
- [ ] **Step 2.1**: Access `https://dev.diet-dost.in` in desktop browser and mobile device.
  - [ ] **Assert**: HTTPS connection established with valid Microsoft Azure Managed Certificate.
  - [ ] **Assert**: TLS 1.3 negotiated with zero browser certificate warnings.
  - [ ] **Assert**: Time to First Byte (TTFB) < 200ms.
- [ ] **Step 2.2**: Inspect HTTP response headers:
  - [ ] **Assert**: Strict-Transport-Security (HSTS) header present.
  - [ ] **Assert**: Content-Security-Policy and X-Content-Type-Options headers active.

---

## 3. Test Suite 2: 5-Tier Showcase Authentication & Zero Deactivation
- [ ] **Step 3.1**: Log in as `free@dietdost.app` (`DietDost@Demo2026!`):
  - [ ] **Assert**: Authentication succeeds; redirected to dashboard.
  - [ ] **Assert**: Tier badge indicates `Free`.
- [ ] **Step 3.2**: Log in as `basic@dietdost.app`:
  - [ ] **Assert**: Authentication succeeds; tier badge indicates `Basic`.
- [ ] **Step 3.3**: Log in as `premium@dietdost.app`:
  - [ ] **Assert**: Authentication succeeds; tier badge indicates `Premium`.
- [ ] **Step 3.4**: Log in as `admin.demo@dietdost.app`:
  - [ ] **Assert**: Authentication succeeds; Admin menu available (read-only tier governance).
- [ ] **Step 3.5**: Log in as `superadmin@dietdost.app`:
  - [ ] **Assert**: Authentication succeeds; SuperAdmin governance and tier editing active.

---

## 4. Test Suite 3: Mobile BFF Live Internet Endpoint Verification
- [ ] **Step 4.1**: Execute `GET https://dev.diet-dost.in/api/mobile/v1/dashboard/composite?period=7D` with valid JWT bearer token.
  - [ ] **Assert**: HTTP 200 OK returned with full `MobileDashboardCompositeDto`.
  - [ ] **Assert**: Response duration < 100ms over public internet.
- [ ] **Step 4.2**: Verify Mobile BFF contract ready for immediate .NET MAUI Phase 2 integration without code changes.

---

## 5. Test Suite 4: SQLite Zero-Data-Loss Durable Restart Verification
- [ ] **Step 5.1**: Log in as `premium@dietdost.app` and log a meal via `/api/meals/analyze-text`:
  - [ ] Meal item: `"1 cup Tadka Dal with 2 multigrain rotis"`.
  - [ ] **Assert**: Meal appears in food diary; caloric ledger increments.
- [ ] **Step 5.2**: Force Azure Container App restart:
  - [ ] Command: `az containerapp restart --name app-dietdost-web --resource-group rg-dietdost-dev`.
  - [ ] Wait for new revision to reach healthy running state.
- [ ] **Step 5.3**: Re-access `https://dev.diet-dost.in` and inspect diary:
  - [ ] **Assert**: Tadka Dal meal remains 100% intact in SQLite database file on persistent Azure Files SMB share.
  - [ ] **Assert**: Zero data loss, zero corrupt database errors, zero write lock deadlocks.

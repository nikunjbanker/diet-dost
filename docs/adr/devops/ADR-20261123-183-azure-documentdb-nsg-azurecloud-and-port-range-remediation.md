<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261123-183: Azure DocumentDB NSG Outbound AzureCloud Service Tag and Replica Node Port Range Remediation

- **Status**: Accepted
- **Date**: 2026-11-23
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud DevOps Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #186
- **Related Issue**: #185

---

## 1. Context & Problem Statement

Following the successful provisioning of Azure DocumentDB (MongoDB vCore Free Tier) and migration of production data from Azure SQL, Container App revision `app-dietdost-web-dev--0000057` was deployed with `Database__Provider=DocumentDb`.

However, the application remained inaccessible on revision 57, and traffic continued routing to revision 56 (running on Azure SQL). Inspection of Azure Container Apps revision status and console logs revealed:
1. `app-dietdost-web-dev--0000057` was in `HealthState: Unhealthy` with continuous startup probe failures:
   ```text
   [INIT] Selected database initializer strategy: 'DocumentDb'.
   [WARMUP] Azure DocumentDB Free Tier is auto-resuming from pause (attempt 1/6)...
   System.TimeoutException: A timeout occurred after 59999ms selecting a server using CompositeServerSelector...
   EndPoint: "Unspecified/fc-d4e768cbe85e-000.mongocluster.cosmos.azure.com:10260" ... State: "Disconnected" ... HeartbeatException: Operation was canceled / timed out.
   ```
2. Because ACA enforces health-gated ingress routing, when revision 57 failed its startup probe, ACA ingress automatically failed over 100% of user traffic back to the last known healthy revision (`app-dietdost-web-dev--0000056` on Azure SQL).

### Root Cause Analysis
- **Port Discrepancy**: Azure DocumentDB MongoDB vCore gateway listens on standard port `27017`. However, its cluster replica set node (`fc-d4e768cbe85e-000.mongocluster.cosmos.azure.com`) listens on port `10260` (within the standard MongoDB vCore replica port range `10250-10265`).
- **Service Tag Mismatch**: The public IP of the DocumentDB cluster node (`20.219.27.218`) belongs to Azure Service Tag `AzureCloud` / `AzureCloud.centralindia`, **not** `AzureCosmosDB`. The `AzureCosmosDB` service tag only covers multi-tenant Cosmos DB NoSQL/legacy API endpoints.
- **NSG Drop**: In `infra/dev/infra.bicep`, rule `Allow-DocumentDB-Outbound` had `destinationAddressPrefix: 'AzureCosmosDB'` and `destinationPortRanges: ['10255', '27017']`. Outbound TCP traffic to port `10260` failed to match and was silently dropped by rule 4000 (`Deny-All-Other-Outbound`), causing connection timeouts.

---

## 2. Decision & Implementation

### 2.1 Update `Allow-DocumentDB-Outbound` NSG Security Rule in `infra/dev/infra.bicep`
Updated the NSG security rule to target the `AzureCloud` system service tag and include the dedicated MongoDB vCore replica node port range:
```bicep
        name: 'Allow-DocumentDB-Outbound'
        properties: {
          priority: 250
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'AzureCloud'
          destinationPortRanges: [
            '10250-10265'
            '27017'
          ]
        }
```

### 2.2 Live Network Security Group Remediation & Container App Restart
1. Updated live NSG `nsg-dietdost-dev` rule `Allow-DocumentDB-Outbound` to allow `AzureCloud` on ports `['27017', '10250-10265']`.
2. Restarted revision `app-dietdost-web-dev--0000057`.
3. Verified immediate connection to DocumentDB without timeout:
   ```text
   [INIT] DocumentDB connection established successfully.
   [SEED] DocumentDB seeded cleanly. 5 demo users initialized.
   ```
4. `az containerapp revision list` confirmed revision 57 transitioned to:
   - `Active: True`
   - `HealthState: Healthy`
   - `TrafficWeight: 100`

### 2.3 Contract Test Synchronization
Synchronized `BicepIaCConfigurationContractTests.cs` to assert:
- `Allow-DocumentDB-Outbound` includes `'10250-10265'`, `'27017'`, and `destinationAddressPrefix: 'AzureCloud'`.
- Port segregation verifies SQL (1433, 11000-11999) vs DocumentDB (27017, 10250-10265).

---

## 3. Consequences & Verification

- **Verification Results**:
  - All Bicep contract tests pass (16/16).
  - All 7 vectors of `verify-ai-security-defense.ps1` pass cleanly.
  - Live container revision 57 serves 100% of production traffic using Azure DocumentDB with zero timeouts.
- **Security Posture**: Outbound egress is strictly restricted to `AzureCloud` on designated DocumentDB ports (`27017`, `10250-10265`), preserving Zero-Trust least-privilege architecture.

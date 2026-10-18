<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-110: Azure Container Apps Consumption Scale-to-Zero Dev Cost Optimization

## Status
Accepted

## Date
2026-10-18

## Context
Analysis of deployed Azure cloud resources in `rg-dietdost-dev` and the auxiliary managed environment resource group `ME_cae-dietdost-dev_rg-dietdost-dev_centralindia` revealed a baseline monthly run rate of **~$43.53 / month (~₹3,678 INR / month)**.

### Cost Breakdown & Bottleneck Analysis:
1. **Container App Compute Dominance**: The Container App (`app-dietdost-web-dev`) configured with `minReplicas: 1` running 24/7 accounted for **~$34.00 / month (~₹2,873 INR / month)**, representing **78.1% of the total monthly cloud bill**.
2. **Azure Consumption Free Grant Underutilization**: Azure Container Apps Consumption plan grants every Azure subscription **180,000 vCPU-seconds, 360,000 GiB-seconds, and 2,000,000 requests completely FREE every single month**. By running `minReplicas: 1` continuously (2,628,000 seconds/month), dev was exhausting the free allowance within the first 4 days and paying full retail rate for the remaining 26 days.
3. **Single-Writer Safety Invariant**: In [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep), `scale` was previously hardcoded to `{ minReplicas: 1, maxReplicas: 1 }`. While `maxReplicas: 1` is strictly mandatory to prevent SQLite multiple-writer database corruption over Azure Files SMB, `minReplicas: 1` is not required during idle hours in non-production environments.

## Decision
1. **Parameterized Scale-to-Zero Configuration in `infra/dev/app.bicep`**:
   - Introduced parameters:
     - `param minReplicas int = (environment == 'dev' ? 0 : 1)`
     - `param maxReplicas int = 1`
   - Bound Container App scale block to:
     ```bicep
     scale: {
       minReplicas: minReplicas
       maxReplicas: maxReplicas // SQLite Zero Data Loss Golden Rule 1 (Single-writer invariant)
     }
     ```
   - Automatically enables scale-to-zero in development (`dev`), allowing the container to idle down to 0 replicas when there is no traffic and fit 100% within the monthly free grant.
   - Enforces `maxReplicas: 1` single-writer invariant unconditionally across all environments.
2. **Live Scale-to-Zero Verification in Azure Cloud**:
   - Updated `app-dietdost-web-dev` in `rg-dietdost-dev` using `az containerapp update --min-replicas 0`.
   - Verified that HTTP requests to `https://dev.diet-dost.in` continue to resolve with HTTP 200 OK and sub-200ms latency.
3. **Contract Test & Architectural Assertion Updates**:
   - Updated `AppBicep_Enforces_SingleReplica_And_PersistentMountPath` in [`BicepIaCConfigurationContractTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs) to validate that `minReplicas` and `maxReplicas` parameters are bound to the scale block and that `maxReplicas` defaults to `1`.
4. **Living Documentation & Skill Synchronization**:
   - Synchronized [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md), [`.agents/skills/diet-dost-azure-deployment/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md), and [`docs/cft/cft_azure_cloud_deployment_aca.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_azure_cloud_deployment_aca.md).

## Consequences
- **Positive**: Lowers dev compute cost from **~₹2,873 / month** to **₹0.00 / month (100% reduction in compute charges)** by leveraging the Azure 180k vCPU-s free grant.
- **Positive**: Reduces the total monthly dev cloud bill from **~₹3,678 / month (~$43.53)** down to **~₹375 / month (~$4.44)** (a **~90% total cost reduction**, run rate ~₹12 / day).
- **Positive**: Preserves SQLite single-writer safety invariant (`maxReplicas: 1`).
- **Trade-off**: A cold-start latency of **~5 to 8 seconds** occurs on the first HTTP request after prolonged idle periods. For development and staging environments, this is completely acceptable.

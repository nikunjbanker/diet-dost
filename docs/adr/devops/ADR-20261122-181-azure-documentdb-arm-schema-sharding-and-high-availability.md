<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261122-181: Azure DocumentDB ARM 2024-07-01 Schema Compliance with Sharding and High Availability

- **Status**: Accepted
- **Date**: 2026-11-22
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud DevOps Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #184
- **Related Issue**: #183

---

## 1. Context & Problem Statement

During live execution of the Azure Infrastructure Provisioning pipeline (`azure-infra-deploy.yml`, Run #45018721727) deploying Azure DocumentDB (Free Tier - Mongo vCore 32 GB), Stage 3 failed with ARM BadRequest error:
```text
Found schema errors for api-version 2024-07-01. Errors: 
The property '#/properties' did not contain a required property of 'sharding' in schema beaeef13-c1ec-5b52-859f-55fd05258103
The property '#/properties' did not contain a required property of 'highAvailability' in schema beaeef13-c1ec-5b52-859f-55fd05258103
```
Under the Azure Resource Manager API specification for `Microsoft.DocumentDB/mongoClusters@2024-07-01`, `sharding` and `highAvailability` are required schema properties. Although the Lifetime Free Tier cluster is a single-node configuration, the ARM schema mandates explicit declaration of the sharding and high-availability blocks.

---

## 2. Decision & Implementation

### 2.1 Explicit Sharding & High Availability Declaration in `infra/dev/documentdb.bicep`
Configured the mandatory ARM properties for `Microsoft.DocumentDB/mongoClusters@2024-07-01`:
```bicep
resource docDbCluster 'Microsoft.DocumentDB/mongoClusters@2024-07-01' = {
  name: actualClusterName
  location: location
  properties: {
    compute: {
      tier: 'Free' // Azure DocumentDB Lifetime Free Tier (32 GB dedicated storage)
    }
    serverVersion: '6.0'
    storage: {
      sizeGb: 32 // 32 GB Dedicated Free Storage
    }
    sharding: {
      shardCount: 1 // Single physical shard for Free Tier cluster
    }
    highAvailability: {
      targetMode: 'Disabled' // High availability disabled for single-node Free Tier
    }
    administrator: {
      userName: adminLogin
      password: adminPassword
    }
  }
}
```

### 2.2 Contract Test Enforcement (`BicepIaCConfigurationContractTests.cs`)
Added automated contract test `DocumentDbBicep_Enforces_FreeTier_Sharding_And_HighAvailability_SchemaCompliance` ensuring that:
1. `tier: 'Free'` and `sizeGb: 32` are asserted.
2. `sharding:` with `shardCount: 1` is declared.
3. `highAvailability:` with `targetMode: 'Disabled'` is declared.

### 2.3 Pre-Flight Live ARM Validation
Executed live ARM API validation via Azure CLI (`az deployment group validate`) against `rg-dietdost-dev`, confirming 0 schema, quota, or parameter errors.

---

## 3. Consequences & Governance

- **100% ARM Schema Compliance**: Eliminates `BadRequest` schema validation errors on `Microsoft.DocumentDB/mongoClusters@2024-07-01`.
- **Zero Cost Invariant**: `shardCount: 1` and `targetMode: 'Disabled'` strictly preserve the Free-for-life tier boundary with 32 GB dedicated storage.
- **Living Synchronization**: Synchronized with `docs/sdd/05_devops_and_infrastructure.md`, `docs/cft/cft_azure_cloud_deployment_aca.md`, and `.agents/skills/diet-dost-azure-deployment/SKILL.md`.

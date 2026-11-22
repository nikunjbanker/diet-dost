<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261122-180: Azure DocumentDB Zero-Trust Network Perimeter Hardening and Port Segregation

- **Status**: Accepted
- **Date**: 2026-11-22
- **Domain**: security
- **Deciders**: Solution Architecture, Cloud Security Architect, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #182
- **Related Issue**: #181

---

## 1. Context & Problem Statement

Adversarial security review of the Azure DocumentDB (`Microsoft.DocumentDB/mongoClusters`) Bicep infrastructure identified a critical network perimeter vulnerability:
1. **Overly Permissive Ingress Rule (`AllowAzureServices`)**: The DocumentDB module initially provisioned a wildcard firewall rule with `startIpAddress: '0.0.0.0'` and `endIpAddress: '0.0.0.0'`. In Azure, this rule permits ingress from **any** compute resource in any subscription across any tenant in all Azure global data centers, directly violating Zero-Trust architecture, Principle of Least Privilege (PoLP), and Checkov security benchmarks (`CKV_AZURE_11`).
2. **Missing Container Apps Egress Alignment**: In Azure SQL (`sql.bicep`), ingress is restricted to the dedicated container apps subnet (`acaSubnetId`) and authorized developer IP addresses. DocumentDB lacked dedicated container egress IP authorization.
3. **Port Segregation and Contract Rigor**: Network documentation and contract tests required explicit formalization of port segregation between Azure SQL (Port 1433, 11000–11999) and Azure DocumentDB (Port 27017, 10255), asserting zero port collisions across firewall and NSG boundaries.

---

## 2. Decision & Implementation

### 2.1 Elimination of Wildcard `0.0.0.0` Firewall Rule by Default
- The unconditional `AllowAzureServices` (`0.0.0.0` - `0.0.0.0`) rule in `infra/dev/documentdb.bicep` is **removed from default deployment**.
- Introduced explicit boolean parameter `param allowAllAzureServices bool = false`. Only if an operator explicitly overrides this parameter with `true` will the wildcard rule be deployed (guarded with `# checkov:skip=CKV_AZURE_11`).

### 2.2 Dedicated Container App Egress IP Authorization (`AllowContainerAppOutboundIp`)
- Added parameter `param containerAppOutboundIp string = ''` to `documentdb.bicep`.
- In `infra/dev/infra.bicep`, the Azure Container Apps environment dedicated outbound public IP (`acaEnvironment.properties.staticIp`) is passed directly to `module documentdb`.
- Provisioned targeted firewall rule `AllowContainerAppOutboundIp` (`startIpAddress: containerAppOutboundIp`, `endIpAddress: containerAppOutboundIp`), ensuring only the Diet-Dost Container App environment can reach the cluster over public routing.
- Preserved developer workstation authorization rule `AllowDeveloperWorkstationIp` (`startIpAddress: allowedClientIp`, `endIpAddress: allowedClientIp`).

### 2.3 Subnet Service Endpoint Integration (`Microsoft.AzureCosmosDB`)
- Updated `snet-aca-infra` subnet in `infra/dev/infra.bicep` to include `Microsoft.AzureCosmosDB` in `serviceEndpoints` alongside `Microsoft.Storage`, `Microsoft.KeyVault`, and `Microsoft.Sql`.
- Ensures traffic from the ACA subnet to Azure Cosmos DB and DocumentDB traverses Microsoft's internal private network backbone.

### 2.4 Strict Port Segregation & Contract Enforcement
- **Azure SQL Ports**: Port `1433` (TCP TDS) + Port Range `11000–11999` (TCP Redirection).
- **Azure DocumentDB Ports**: Port `27017` (TCP native MongoDB wire protocol) + Port `10255` (TCP legacy gateway).
- **Disjoint Invariant**:
  $$\text{Ports}_{\text{SQL}} \cap \text{Ports}_{\text{DocumentDB}} = \emptyset$$
- Exported explicit `databasePort` outputs (`1433` for SQL, `27017` for DocumentDB) across `sql.bicep`, `documentdb.bicep`, `infra.bicep`, and `main.bicep`.
- Added contract tests in `BicepIaCConfigurationContractTests.cs` verifying:
  - Wildcard `0.0.0.0` rule rejection by default.
  - Presence of `AllowContainerAppOutboundIp` and `AllowDeveloperWorkstationIp`.
  - Disjoint port configuration in NSG outbound rules.
  - Subnet service endpoints declaration for both `Microsoft.Sql` and `Microsoft.AzureCosmosDB`.

---

## 3. Consequences & Governance

- **Zero-Trust Hardening**: Eliminates unauthorized cross-tenant Azure ingress to the DocumentDB database cluster.
- **Port Isolation Assurance**: Guarantees distinct port allocation between relational and document database engines.
- **Automated Verification**: Enforced by 15 passing contract tests in `BicepIaCConfigurationContractTests.cs` and pre-commit AI Security Defense gates.

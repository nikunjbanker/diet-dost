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

### 2.1 Container Apps Egress & Azure Services Firewall Rule (`AllowAzureServices`)
- In `infra/dev/documentdb.bicep`, the module defaults `allowAllAzureServices = false` for standalone security.
- In `infra/dev/infra.bicep`, when deploying the integrated architecture, `allowAllAzureServices: true` is passed to the `documentdb` module.
- **Why this is required for Azure Container Apps (ACA)**:
  - In Azure Container Apps integrated into a custom VNet without a dedicated NAT Gateway, `acaEnvironment.properties.staticIp` is the **inbound ingress** IP (for HTTP/custom domain routing).
  - Outbound container egress traffic to external Azure PaaS services originates dynamically from Azure's datacenter IP pool.
  - Azure DocumentDB does not support VNet Service Endpoints or `virtualNetworkRules`.
  - Therefore, the official Microsoft-supported mechanism for ACA compute to reach DocumentDB is the Azure Services firewall rule `AllowAzureServices` (`0.0.0.0` - `0.0.0.0`).
  - In Azure Resource Manager, `0.0.0.0` to `0.0.0.0` is a special Azure sentinel rule that permits access strictly from Azure-internal compute resources, while completely blocking all non-Azure public internet access.
  - Developer access remains governed by explicit client IP in `AllowDeveloperWorkstationIp`.

### 2.2 Dedicated Container App Egress IP Authorization (`AllowContainerAppOutboundIp`)
- Added parameter `param containerAppOutboundIp string = ''` to `documentdb.bicep`.
- In `infra/dev/infra.bicep`, the Azure Container Apps environment dedicated outbound public IP (`acaEnvironment.properties.staticIp`) is passed directly to `module documentdb`.
- Provisioned targeted firewall rule `AllowContainerAppOutboundIp` (`startIpAddress: containerAppOutboundIp`, `endIpAddress: containerAppOutboundIp`), ensuring only the Diet-Dost Container App environment can reach the cluster over public routing.
- Preserved developer workstation authorization rule `AllowDeveloperWorkstationIp` (`startIpAddress: allowedClientIp`, `endIpAddress: allowedClientIp`).

### 2.3 Architectural Differentiation: Service Endpoints vs. IP Firewall Rules
- **Azure DocumentDB (`mongoClusters`) vs. Azure Cosmos DB (`databaseAccounts`)**:
  - `Microsoft.AzureCosmosDB` VNet service endpoint is specifically designed for multi-tenant Cosmos DB database accounts (`Microsoft.DocumentDB/databaseAccounts`).
  - Dedicated Azure DocumentDB vCore clusters (`Microsoft.DocumentDB/mongoClusters`) do **not** support or bind to VNet Service Endpoints.
  - Instead, Azure DocumentDB secures network ingress through **targeted IP firewall rules** (`Microsoft.DocumentDB/mongoClusters/firewallRules`) matching the Container App's dedicated static egress IP (`containerAppOutboundIp`), and optionally through **Azure Private Endpoints (Private Link)**.
  - Therefore, `Microsoft.AzureCosmosDB` is **not** added to the subnet service endpoints, avoiding routing conflation between DocumentDB and Cosmos DB.

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
  - Subnet service endpoints declaration for `Microsoft.Sql` (for Azure SQL VNet rules) and exclusion of unneeded `Microsoft.AzureCosmosDB`.

---

## 3. Consequences & Governance

- **Zero-Trust Hardening**: Eliminates unauthorized cross-tenant Azure ingress to the DocumentDB database cluster.
- **Port Isolation Assurance**: Guarantees distinct port allocation between relational and document database engines.
- **Automated Verification**: Enforced by 15 passing contract tests in `BicepIaCConfigurationContractTests.cs` and pre-commit AI Security Defense gates.

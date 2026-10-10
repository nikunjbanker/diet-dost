<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261010-088: Azure Zero-Trust Network Security Group, SMB 3.1.1 Channel Encryption, and Diagnostic Auditing Hardening

- **Status**: Accepted
- **Date**: 2026-10-10
- **Domain**: Security & Cloud Infrastructure Architecture
- **Author**: Diet-Dost Core Architectural Pair
- **Decision Makers**: `@nikunjbanker` (Sole Authority)

## 1. Context and Problem Statement
Following comprehensive analysis from an **Azure Network & Security Expert** perspective, foundational infrastructure (`infra/infra.bicep` and `infra/app.bicep`) required hardening against enterprise cloud benchmarks (CIS Azure Foundations, Microsoft Cloud Security Benchmark / Azure Security Benchmark v3) while strictly preserving dev-test **free-tier cost containment invariants**:
1. **Unprotected VNet Subnet**: The delegated subnet `snet-aca-infra` lacked an associated Network Security Group (NSG), leaving container instances exposed to open internal subnet traffic and unmonitored egress.
2. **Unenforced SMB Protocol Encryption**: Azure Files SMB traffic lacked explicit protocol version and cipher suite enforcement, leaving SQLite database network I/O vulnerable to downgrade or unencrypted traffic.
3. **Database Share Accidental Deletion Risk**: File shares lacked a soft-delete retention policy, leaving `dietdost-data` (holding `diet_dost.db`) vulnerable to immediate destruction on deletion.
4. **Missing Key Vault Audit Diagnostics**: Key Vault secret access events were not streamed to Log Analytics, preventing forensic auditing of credential retrievals.
5. **No Proactive Threat Detection Alerts**: No automated alerts monitored unauthorized access attempts (HTTP 401/403 spikes) against Key Vault.
6. **Cost Constraint**: The environment is dev-test; all security hardening measures MUST avoid paid premium tiers (e.g. keeping ACR Basic, standard Key Vault, Standard_LRS storage, free-tier platform metric alerts).

## 2. Decision and Implementation

### 2.1 Zero-Trust Network Security Group (`nsg-dietdost-${environment}`)
Added `Microsoft.Network/networkSecurityGroups@2023-05-01` attached directly to `snet-aca-infra`:
- **Inbound Rules**:
  - `Allow-HTTP-HTTPS-Inbound` (Priority 100): Allows TCP 80 and 443 for managed TLS ingress.
  - `Allow-AzureLoadBalancer-Inbound` (Priority 110): Allows health probe traffic from `AzureLoadBalancer`.
  - Default Azure NSG rules block all other inbound traffic.
- **Outbound Rules**:
  - `Allow-Storage-SMB-Outbound` (Priority 100): Port 445 scoped strictly to the `Storage` service tag.
  - `Allow-AzureCloud-HTTPS-Outbound` (Priority 110): Port 443 scoped to `AzureCloud` (ACR, Key Vault, Entra ID).
  - `Allow-DNS-Outbound` (Priority 120): Port 53 UDP/TCP for internal and external name resolution.
  - `Allow-NTP-Outbound` (Priority 130): Port 123 UDP for Linux container clock synchronization.
  - `Allow-Internet-HTTPS-Outbound` (Priority 140): Port 443 to `Internet` for external multimodal AI Vision APIs (Google Gemini).

### 2.2 Azure Files SMB 3.1.1 Channel Encryption & Soft-Delete Retention
Configured `fileServices` in `infra/infra.bicep`:
- Enforced `SMB3.1.1` protocol with `AES-128-GCM;AES-256-GCM` encryption ciphers.
- Enabled `shareDeleteRetentionPolicy` with `days: 7`, guaranteeing that accidental deletion or malicious share removal can be restored within 7 days.

### 2.3 Key Vault Audit Diagnostic Logging & Monitoring
Configured `Microsoft.Insights/diagnosticSettings@2021-05-01-preview` on `keyVault`:
- Streams `AuditEvent` and `AzurePolicyEvaluationDetails` logs alongside all metrics into `logAnalytics`.
- Enables continuous SIEM auditing, tracking every secret read operation with timestamps, caller identity, and client IP.

### 2.4 Automated Security Metric Alert for Unauthorized Access
Configured `Microsoft.Insights/metricAlerts@2018-03-01`:
- Monitors `ServiceApiResult` on Key Vault where status code is `401` or `403`.
- Fires Severity 1 alert when unauthorized requests exceed 5 in a 5-minute window.
- Fully free: utilizes the 10 free metric alert rules per month included with every Azure subscription.

### 2.5 Ingress Perimeter IP Security Restrictions
Configured `infra/app.bicep`:
- Added `ipSecurityRestrictions` parameter to `ingress`, enabling granular whitelisting of trusted management IPs, reverse proxies, or Cloudflare/Front Door CIDRs.

## 3. Consequences

### Positive
- **Zero Cost Increase**: All 5 enhancements utilize free Azure features (NSG: $0, SMB 3.1.1: $0, Log Analytics within 5 GB/mo free grant, Basic platform metric alert: $0, IP restrictions: $0).
- **CIS & Benchmark Compliance**: Fulfills CIS Azure Foundations Subnet NSG rule (`CKV_AZURE_9`) and Azure Security Benchmark logging guidelines.
- **Ransomware & Loss Protection**: 7-day file share soft-delete prevents unrecoverable SQLite database destruction.
- **Tamper-Evident Secret Access**: Every access to Azure Key Vault is forensically recorded in Log Analytics.

### Forward Roadmap Impact & Future Phase Compatibility
- Architecturally prepares Diet-Dost for Phase 3 Enterprise Cloud Persistence and GA 1.0 Production transition where Private Endpoints and Azure Front Door WAF can be attached seamlessly without modifying application code or CQRS handlers.

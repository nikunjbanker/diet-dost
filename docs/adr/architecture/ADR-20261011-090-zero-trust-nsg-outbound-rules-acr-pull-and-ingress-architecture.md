<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261011-090: Azure Zero-Trust NSG Outbound Rules, Passwordless ACR Image Pull, and Direct ACA Ingress Architecture

- **Status**: Accepted
- **Date**: 2026-10-11
- **Domain**: Architecture & Infrastructure Security
- **Author**: Diet-Dost Solution Architecture Team
- **Decision Makers**: `@nikunjbanker` (Sole Authority)
- **Consulted**: Application, Infrastructure, and DevSecOps Teams
- **Informed**: All Contributors, Autonomous AI Agents

---

## 1. Context and Problem Statement

During an architecture and visual artifact audit of the Diet-Dost Azure cloud perimeter ([`docs/architecture/diagrams/azure_zero_trust_infra_architecture.jpg`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/azure_zero_trust_infra_architecture.jpg) and [`infra/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/infra.bicep)), several critical architectural clarifications and security hardening requirements were identified:

1. **Permissive Outbound Fallthrough Risk in NSG**:
   - The Network Security Group (`nsg-dietdost-dev`) defined explicit allow rules (priority 100-140) for Storage SMB (445), AzureCloud HTTPS (443), DNS (53), NTP (123), and Internet HTTPS (443), but **lacked an explicit catch-all `Deny` rule for outbound traffic**.
   - In Azure NSG architecture, unlisted outbound traffic falls through to the Azure default rule `AllowInternetOutBound` (priority 65001), which permits all egress traffic across any arbitrary port (e.g. SSH 22, Telnet 23, SMTP 25, IRC 6667, reverse shells). This violated strict Zero-Trust defense-in-depth principles.
2. **Architectural Hallucination in Visual Diagram (Front Door & Application Gateway)**:
   - The previous visual diagram displayed Azure Front Door and Azure Application Gateway.
   - In reality, Diet-Dost does **not** provision or utilize Azure Front Door or Application Gateway. External HTTPS traffic routes directly to the Azure Container Apps (ACA) built-in Envoy reverse proxy layer with free Azure Managed TLS 1.3 certificates terminating at the ACA boundary.
3. **Ambiguity in Container Image Delivery Mechanics**:
   - The image pull relationship between Azure Container Registry (ACR) and the Container App was not visually clear in the diagram, obscuring how the workload authenticates to ACR without admin credentials.
4. **Pre-Deployment Script & Workflow Secret Alignment**:
   - `scripts/setup-azure-pre-deployment.ps1` was setting `SUPER_ADMIN_EMAIL` and `REQUIRE_MOBILE_VERIFICATION` as plaintext repository variables (`vars`), whereas `.github/workflows/azure-app-deploy.yml` Step 10b strictly expects encrypted secrets (`secrets.`) per [ADR-077](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-077-secret-only-governance-for-superadmin-email-and-mobile-verification.md). This would have caused the deployed container to fail startup validation in production mode.
   - Additionally, `azure-app-deploy.yml` Step 4c attempted to run ESLint with the obsolete `.eslintrc.js` configuration file.

---

## 2. Decision and Implementation

### 2.1 Explicit Zero-Trust Catch-All Deny Rules in Bicep (`infra/infra.bicep`)
Hardened the NSG configuration in [`infra/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/infra.bicep) by adding explicit high-priority deny rules and VNet internal communication rules:
- **Inbound Security Rules**:
  - `100: Allow-HTTPS-Inbound` (TCP 443 from `*`)
  - `110: Allow-AzureLoadBalancer-Inbound` (Health probes from `AzureLoadBalancer`)
  - `120: Allow-VNet-Internal-Inbound` (Intra-VNet cluster communication within `VirtualNetwork`)
  - **`4000: Deny-All-Other-Inbound`** (Explicit catch-all deny rule dropping all unauthorized inbound traffic)
- **Outbound Security Rules**:
  - `100: Allow-Storage-SMB-Outbound` (TCP 445 to `Storage` service tag for persistent SQLite mount)
  - `110: Allow-AzureCloud-HTTPS-Outbound` (TCP 443 to `AzureCloud` service tag for Key Vault, ACR, Entra ID, Log Analytics)
  - `120: Allow-DNS-Outbound` (Port 53 for DNS resolution)
  - `130: Allow-NTP-Outbound` (UDP 123 for time synchronization)
  - `140: Allow-Internet-HTTPS-Outbound` (TCP 443 to `Internet` for Google AI Gemini API)
  - `150: Allow-VNet-Internal-Outbound` (Intra-VNet cluster communication within `VirtualNetwork`)
  - **`4000: Deny-All-Other-Outbound`** (Explicit catch-all deny rule overriding Azure's permissive default `AllowInternetOutBound` 65001)

### 2.2 Direct ACA Managed Envoy Ingress Architecture
Codified the zero-intermediate-hop ingress architecture:
- External traffic from `dev.diet-dost.in` routes directly via DNS (CNAME + TXT) to the ACA Managed Environment's built-in Envoy proxy.
- **Zero Front Door & Zero Application Gateway**: Eliminates unnecessary cloud cost (~$18-35/month) and network hops while providing free managed TLS 1.3 certificates directly through ACA.
- Configurable IP security restrictions are enforced directly at the ACA ingress level via `ipSecurityRestrictions` in [`infra/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/app.bicep).

### 2.3 Passwordless ACR OCI Image Pull Governance
Documented and validated the passwordless image delivery pipeline:
- **Registry Hardening**: Azure Container Registry (`crdietdost*`) runs with `adminUserEnabled: false` (zero static admin passwords).
- **Managed Identity Authorization**: A User-Assigned Managed Identity (`id-dietdost-dev`) is assigned the Azure RBAC built-in role **`AcrPull`** (`7f951dda-4ed3-4680-a7ca-43fe172d538d`) scoped directly to the ACR instance.
- **Platform Image Pull**: When deploying or scaling revisions, Azure Container Apps uses the Managed Identity OAuth token to pull OCI container images over HTTPS (port 443, permitted by `Allow-AzureCloud-HTTPS-Outbound`).
- **CI/CD Push**: GitHub Actions builds the image using Podman 5.7.0 and logs in via ephemeral OAuth tokens (`az acr login --expose-token`).

### 2.4 Pre-Deployment Automation & Workflow Alignment
1. **[`scripts/setup-azure-pre-deployment.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/setup-azure-pre-deployment.ps1)**:
   - Removed `SUPER_ADMIN_EMAIL` and `REQUIRE_MOBILE_VERIFICATION` from plaintext GitHub variables (`vars`).
   - Moved them to encrypted GitHub Secrets (`secrets.SUPER_ADMIN_EMAIL`, `secrets.REQUIRE_MOBILE_VERIFICATION`) per ADR-077.
   - Added automatic cleanup of legacy plaintext variables (`gh variable delete`).
   - Added dynamic Git branch resolution (`git branch --show-current`, fallback `main`).
   - Added support for `AZURE_OPENAI_API_KEY`.
2. **[`azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml)**:
   - Modernized Step 4c to ESLint 9+ flat configuration (`eslint@9.20.0` and `npx eslint .`).
   - Upgraded Gitleaks to `v8.30.1` in both `azure-app-deploy.yml` and `azure-infra-deploy.yml`.

### 2.5 Visual Artifact & Mermaid Synchronization
- Regenerated high-resolution architecture diagram [`docs/architecture/diagrams/azure_zero_trust_infra_architecture.jpg`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/azure_zero_trust_infra_architecture.jpg) with accurate direct ACA ingress, explicit NSG Inbound/Outbound Zero-Trust panels, ACR AcrPull image pull flow, and zero Front Door / Application Gateway components.
- Synchronized canonical Mermaid source [`docs/architecture/diagrams/azure_zero_trust_infra_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/azure_zero_trust_infra_architecture.mermaid).
- Validated 6/6 canonical architecture diagrams with `python3 scripts/sync-architecture-diagrams.py`.

---

## 3. Consequences

### Positive
- **True Zero-Trust Egress**: Outbound connections on non-authorized ports (e.g. port 22, port 80, arbitrary reverse shells) are dropped deterministically by rule 4000.
- **Zero Cost Ingress Overhead**: Confirmed elimination of unnecessary Azure Front Door and Application Gateway services, saving ~$25-40/month.
- **Fail-Safe Startup**: Pre-deployment script now sets `SUPER_ADMIN_EMAIL` in Key Vault via GitHub Secrets, ensuring container startup validation in production mode passes without `InvalidOperationException`.
- **Zero Documentation Drift**: Mermaid source, high-res diagram, SDD documentation, and agent skills are 100% aligned with Bicep and runtime reality.

---

## 4. Verification Evidence

1. `az bicep build --file infra/infra.bicep`: Exited with code 0 (valid compilation).
2. `pwsh -File scripts/setup-azure-pre-deployment.ps1 -DryRun`: Verified staging of `SUPER_ADMIN_EMAIL` and `REQUIRE_MOBILE_VERIFICATION` as encrypted secrets.
3. `python3 scripts/sync-architecture-diagrams.py`: Verified 6/6 diagrams passing semantic and syntax validation.
4. `dotnet test`: 242/242 tests passing with 0 warnings and 0 errors.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-096: Aspire Cloud Infrastructure Provisioning Modernization (Three-Stage Pipeline & Immutable GitHub OIDC Federated Credentials)

> **ADR ID**: `ADR-20261017-096-aspire-azure-infra-oidc-and-three-stage-provisioning`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-17`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[DEVOPS]`, `[SECURITY]`, `[ARCHITECTURE]`  
> **Impacted Files**: [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml), [`scripts/setup-azure-pre-deployment.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/setup-azure-pre-deployment.ps1), [`docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md), [`.agents/skills/diet-dost-azure-deployment/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md), [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md)  
> **Related Documents**: [`ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md), [`ADR-20261010-086-azure-pre-deployment-powershell-automation-and-configuration-runbook.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261010-086-azure-pre-deployment-powershell-automation-and-configuration-runbook.md)

---

## 1. Executive Summary & Problem Context

During execution of the Aspire Cloud Infrastructure Provisioning workflow ([Run 39008596498 / Job 117327016911](https://github.com/nikunjbanker/diet-dost/actions/runs/39008596498/job/117327016911)), Step 5 (`Azure CLI Authentication (OIDC / Federated Credentials)`) aborted with:
```text
AADSTS700213: No matching federated identity record found for presented assertion subject 
'repo:nikunjbanker@3224683/diet-dost@1371432061:environment:dev'. Check your federated identity 
credential Subject, Audience and Issuer against the presented assertion.
```

### 1.1 Root Cause: GitHub Immutable OIDC Subject Claims
GitHub Actions on `github.com` upgraded default OpenID Connect (OIDC) token subject claims to protect against repository transfer, rename, and account recycling vulnerabilities. Rather than issuing purely mutable name-based claims (`repo:<owner>/<repo>:environment:<env>`), GitHub appends the immutable numerical owner ID (`@3224683`) and repository ID (`@1371432061`):
`repo:nikunjbanker@3224683/diet-dost@1371432061:environment:dev`

Because Microsoft Entra ID enforces exact string equality on token subjects, the pre-existing credential configured with only the classic format (`repo:nikunjbanker/diet-dost:environment:dev`) was rejected.

### 1.2 Architectural Deficiency: Monolithic Infrastructure Deployment
Previously, `.github/workflows/azure-infra-deploy.yml` executed cloud provisioning in a single monolithic job (`provision-infrastructure`). There was no preview mechanism, pre-flight ARM validation, or separation of concerns. Operators had no visibility into what ARM resources would be mutated until deployment had already commenced.

---

## 2. Architectural Decisions & Implementation

### 2.1 Resolution of OIDC Federated Identity Credentials
1. **Entra ID App Registration Credential Update**:
   Configured immutable federated identity credentials on the deployment Service Principal (`sp-dev-dietdost-github-deployer`, App ID `35ae72c4-4f7c-4a26-a589-c805f168db32`):
   - `gh-dietdost-dev-env-immutable`: `repo:nikunjbanker@3224683/diet-dost@1371432061:environment:dev`
   - `gh-dietdost-main-branch-immutable`: `repo:nikunjbanker@3224683/diet-dost@1371432061:ref:refs/heads/main`
   - `gh-dietdost-prod-env-immutable`: `repo:nikunjbanker@3224683/diet-dost@1371432061:environment:production`
2. **Automated Dynamic Pre-Deployment Tooling (`scripts/setup-azure-pre-deployment.ps1`)**:
   Enhanced the PowerShell configuration automation to query numeric owner and repo IDs via `gh api repos/$GitHubRepo --jq '{owner_id: .owner.id, repo_id: .id}'` and automatically register both classic and immutable federated credentials. Sanitized branch naming rules prevent special character rejections.

### 2.2 Three-Stage Sequential Infrastructure Pipeline
Decomposed the cloud infrastructure pipeline in [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml) into three sequential, observable stages:

```mermaid
graph TD
    subgraph PreGates ["Pre-Provisioning Security & Quality Gates"]
        Sec[security-scan.yml<br/>8-Job Security Suite]
        Quality[quality-test-gate.yml<br/>4-Tier Test Suite]
    end

    subgraph Stage1 ["Stage 1: Validate (validate-infrastructure)"]
        DotNet[Setup .NET 11 & Aspire CLI]
        Leaks[Gitleaks Secret Scan]
        BicepBuild[Bicep Syntax & Linter Compilation]
        AspirePublish[Aspire Publish Manifest Evaluation]
        AzLogin1[Azure CLI OIDC Login]
        EnsureRG[Ensure Resource Group Exists]
        ArmValidate[az deployment group validate<br/>Live ARM Pre-Flight Schema & Quota]
        DotNet --> Leaks --> BicepBuild --> AspirePublish --> AzLogin1 --> EnsureRG --> ArmValidate
    end

    subgraph Stage2 ["Stage 2: Preview (preview-infrastructure)"]
        AzLogin2[Azure CLI OIDC Login]
        WhatIf[az deployment group what-if<br/>Planned Mutation Detection]
        Summary2[Publish What-If Matrix to $GITHUB_STEP_SUMMARY]
        AzLogin2 --> WhatIf --> Summary2
    end

    subgraph Stage3 ["Stage 3: Provision (provision-infrastructure)"]
        AzLogin3[Azure CLI OIDC Login]
        ArmDeploy[Azure/arm-deploy@v2<br/>infra/infra.bicep]
        EndpointVerify[Verify Endpoints & Health]
        Summary3[Publish Final Provisioning Table to $GITHUB_STEP_SUMMARY]
        AzLogin3 --> ArmDeploy --> EndpointVerify --> Summary3
    end

    Sec --> Stage1
    Quality --> Stage1
    Stage1 --> Stage2
    Stage2 --> Stage3
```

#### Detailed Stage Specifications:
1. **Stage 1: Validate (`validate-infrastructure`)**:
   - Compiles and evaluates Aspire AppHost application model (`aspire publish --apphost src/Nutrition.AppHost`).
   - Verifies zero secret leaks via Gitleaks (`gitleaks detect`).
   - Compiles Bicep templates (`az bicep build --file infra/infra.bicep` and `infra/app.bicep`).
   - Authenticates via Azure OIDC.
   - Runs live ARM API validation (`az deployment group validate`) ensuring zero quota or ARM schema violations before any deployment action.
2. **Stage 2: Preview (`preview-infrastructure`)**:
   - Executes Azure Resource Manager What-If change detection (`az deployment group what-if`).
   - Captures planned resource creations, modifications, deletions, or no-op states.
   - Attaches an interactive Markdown preview matrix to `$GITHUB_STEP_SUMMARY` for developer review.
3. **Stage 3: Provision (`provision-infrastructure`)**:
   - Executes deployment of `infra/infra.bicep` via `Azure/arm-deploy@v2`.
   - Provisions ACA Environment, Azure Files SMB Share (`dietdost-data`), Storage Mount (`dietdoststorage`), Key Vault, Log Analytics, User-Assigned Managed Identity, and ACR.
   - Verifies endpoint health and emits connection metadata into step summary.

---

## 3. Verification & Validation Evidence

1. **Active Entra ID Verification**:
   - Verified that `gh-dietdost-dev-env-immutable`, `gh-dietdost-main-branch-immutable`, and `gh-dietdost-prod-env-immutable` are registered with `c7509a2b-d3e0-46c8-ab12-9d07b9530120`.
2. **Local Bicep Compilation**:
   - `az bicep build --file infra/infra.bicep` -> Exited with code 0.
   - `az bicep build --file infra/app.bicep` -> Exited with code 0.
3. **Automated Setup Script Dry-Run**:
   - `pwsh -File scripts/setup-azure-pre-deployment.ps1 -DryRun -SkipAzLogin` -> Succeeded with 0 errors, validating all 4 Entra ID federated credentials and RBAC roles.
4. **Local Multi-Tier CFT Suite**:
   - Launched WebGateway on port 5240 and executed `pwsh -File tests/validate_e2e_tiers.ps1`.
   - All 5 user tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`) passed with 100% success rate.
5. **Unit & Evaluation Test Suites**:
   - `dotnet test --configuration Release` -> 243 passed, 0 failed, 0 skipped.

---

## 4. Consequences & Impact

- **Zero OIDC Authentication Failures**: Eliminates `AADSTS700213` by honoring GitHub's immutable owner/repo ID subject claim standards.
- **Enterprise Deployment Visibility**: Three distinct stages provide visual step summaries and a clear separation between validation, preview, and deployment.
- **Zero Accidental Resource Drift**: What-If preview guarantees that unintended deletions or infrastructure replacements are detected prior to live execution.

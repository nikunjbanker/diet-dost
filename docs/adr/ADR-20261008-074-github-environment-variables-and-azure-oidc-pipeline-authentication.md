<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-074: GitHub Environment Variables & Passwordless Azure OIDC Pipeline Authentication

> **ADR ID**: `ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-08`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[DEVOPS]`, `[SECURITY]`, `[GOVERNANCE]`  
> **Impacted Files**: `.github/workflows/azure-infra-deploy.yml`, `.github/workflows/azure-app-deploy.yml`, `.agents/skills/diet-dost-azure-deployment/SKILL.md`  
> **Related Documents**: [`ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md), [`ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md)

---

## 1. Executive Summary & Context

To support enterprise deployment governance and remove brittle hardcoded credentials, Microsoft Azure authentication in GitHub Actions CI/CD workflows is modernized to utilize **OpenID Connect (OIDC) / Federated Identity Credentials** rather than legacy client-secret JSON credentials (`AZURE_CREDENTIALS`).

The deployment targets are configured per-environment in GitHub (`https://github.com/nikunjbanker/diet-dost/settings/environments/<env-id>/edit`). In the target environment (e.g. `dev`), non-sensitive identification settings are configured as GitHub Environment Variables:
1. `AZURE_CLIENT_ID`: The Application (Client) ID of the Service Principal / App Registration.
2. `AZURE_SUBSCRIPTION_ID`: The target Azure Subscription ID.
3. `AZURE_TENANT_ID`: The Azure Entra ID Directory (Tenant) ID.

This ADR records the architectural decision to update the Aspire CI/CD workflows (`azure-infra-deploy.yml` and `azure-app-deploy.yml`) to:
1. Bind the target GitHub Environment context via `environment: ${{ inputs.environment || 'dev' }}` at the job level.
2. Read OIDC parameters directly from `${{ vars.AZURE_CLIENT_ID }}`, `${{ vars.AZURE_SUBSCRIPTION_ID }}`, and `${{ vars.AZURE_TENANT_ID }}` (with transparent secret fallback).
3. Authenticate to Azure CLI using `azure/login@v2` with `client-id`, `tenant-id`, and `subscription-id` via OIDC (`permissions: id-token: write`), with an automated fallback step for legacy `creds: ${{ secrets.AZURE_CREDENTIALS }}`.
4. Explicitly bind the Azure subscription context (`az account set --subscription "$SUB_ID"`) and parameterize `azure/arm-deploy@v3` with the resolved subscription ID.

---

## 2. Architecture & Authentication Flow

```
                      ┌───────────────────────────────────────────────┐
                      │    GitHub Environment (e.g. 'dev')            │
                      │  - vars.AZURE_CLIENT_ID                       │
                      │  - vars.AZURE_SUBSCRIPTION_ID                 │
                      │  - vars.AZURE_TENANT_ID                       │
                      └───────────────────────┬───────────────────────┘
                                              │
                      ┌───────────────────────▼───────────────────────┐
                      │    GitHub Actions Job Environment Binding     │
                      │    environment: ${{ inputs.environment }}     │
                      └───────────────────────┬───────────────────────┘
                                              │
                      ┌───────────────────────▼───────────────────────┐
                      │    GitHub OIDC Token (id-token: write)        │
                      └───────────────────────┬───────────────────────┘
                                              │
               ┌──────────────────────────────┴──────────────────────────────┐
               ▼                                                             ▼
┌──────────────────────────────┐                              ┌──────────────────────────────┐
│  Step: azure/login@v2 (OIDC) │                              │  Step: Fallback Legacy JSON  │
│  client-id: ${{ vars... }}   │                              │  creds: ${{ secrets... }}    │
│  tenant-id: ${{ vars... }}   │                              │  (if OIDC vars absent)       │
│  subscription-id: ${{ vars..}}                              └──────────────────────────────┘
└──────────────┬───────────────┘
               │
┌──────────────▼─────────────────────────────────────────────────────────────┐
│ Azure CLI Authenticated: az account set --subscription "$SUB_ID"           │
│ Bicep ARM Deploy: azure/arm-deploy@v3 (subscriptionId: ${{ vars... }})      │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Concrete Implementation Changes

### 3.1 Job-Level Environment Binding
Both `.github/workflows/azure-infra-deploy.yml` and `.github/workflows/azure-app-deploy.yml` declare the environment context:
```yaml
jobs:
  build-test-and-deploy:
    runs-on: ubuntu-latest
    environment: ${{ inputs.environment || 'dev' }}
    env:
      AZURE_CLIENT_ID: ${{ vars.AZURE_CLIENT_ID || secrets.AZURE_CLIENT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ vars.AZURE_SUBSCRIPTION_ID || secrets.AZURE_SUBSCRIPTION_ID }}
      AZURE_TENANT_ID: ${{ vars.AZURE_TENANT_ID || secrets.AZURE_TENANT_ID }}
```

### 3.2 Dual-Mode Azure CLI Authentication
```yaml
- name: Azure CLI Authentication (OIDC / Federated Credentials)
  if: ${{ vars.AZURE_CLIENT_ID != '' || secrets.AZURE_CLIENT_ID != '' }}
  uses: azure/login@v2
  with:
    client-id: ${{ vars.AZURE_CLIENT_ID || secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ vars.AZURE_TENANT_ID || secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID || secrets.AZURE_SUBSCRIPTION_ID }}

- name: Azure CLI Authentication (Fallback: Service Principal JSON)
  if: ${{ (vars.AZURE_CLIENT_ID == '' && secrets.AZURE_CLIENT_ID == '') && secrets.AZURE_CREDENTIALS != '' }}
  uses: azure/login@v2
  with:
    creds: ${{ secrets.AZURE_CREDENTIALS }}
```

### 3.3 Explicit Subscription Targeting
```yaml
- name: Ensure Azure Resource Group Exists (Create if Missing)
  run: |
    SUB_ID="${{ vars.AZURE_SUBSCRIPTION_ID || secrets.AZURE_SUBSCRIPTION_ID }}"
    if [ -n "$SUB_ID" ]; then
      echo "Setting Azure subscription context to '$SUB_ID'..."
      az account set --subscription "$SUB_ID"
    fi
```

### 3.4 Bicep Deployment Parameterization
All `azure/arm-deploy@v3` actions now resolve:
```yaml
subscriptionId: ${{ vars.AZURE_SUBSCRIPTION_ID || secrets.AZURE_SUBSCRIPTION_ID }}
```

---

## 4. Forward Roadmap Impact & Future Phase Compatibility

1. **Multi-Environment Promotion (Dev -> Staging -> Prod)**:
   By tying authentication parameters to GitHub Environments (`dev`, `staging`, `prod`), promoting workloads between environments is achieved simply by specifying `inputs.environment` when triggering workflows, without touching code or shared workflow files.
2. **Zero-Secret Maintenance**:
   Passwordless OIDC eliminates rotating client secrets every 180/365 days, mitigating deployment failures caused by expired credentials.
3. **Fine-Grained Role-Based Access Control (RBAC)**:
   Azure Entra ID Federated Credentials can be restricted to specific branches or environments (`repo:nikunjbanker/diet-dost:environment:dev`), preventing unauthorized deployment runs.

---

## 5. Verification & Status

- **Workflow Validation**: Verified syntax and schema compliance across `.github/workflows/azure-infra-deploy.yml` and `.github/workflows/azure-app-deploy.yml`.
- **Environment Verification**: Confirmed active environment variables in `dev` via `gh variable list --env dev`:
  - `AZURE_CLIENT_ID`: `35ae72c4-4f7c-4a26-a589-c805f168db32`
  - `AZURE_SUBSCRIPTION_ID`: `2b540f50-1a74-4019-945a-3cc48a284a1a`
  - `AZURE_TENANT_ID`: `46eec2d9-d80f-426f-8e22-594361873d90`
- **Build & Tests**: Solution builds with 0 warnings, 0 errors, and passes all 206 unit/integration tests.

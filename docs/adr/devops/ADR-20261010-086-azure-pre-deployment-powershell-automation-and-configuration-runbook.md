<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-086: Azure Pre-Deployment PowerShell Automation & Master Configuration Runbook

> **ADR ID**: `ADR-20261010-086-azure-pre-deployment-powershell-automation-and-configuration-runbook`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-10`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[DEVOPS]`, `[SECURITY]`, `[GOVERNANCE]`  
> **Impacted Files**: [`docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md), [`scripts/setup-azure-pre-deployment.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/setup-azure-pre-deployment.ps1), [`docs/AZURE_DEVOPS_DEPLOYMENT_TODO.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_DEVOPS_DEPLOYMENT_TODO.md), [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md)  
> **Related Documents**: [`ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md), [`ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md)

---

## 1. Executive Summary & Context

Prior deployment runbooks contained bash commands that failed or caused friction on Windows-based operator workstations (e.g. quote-stripping issues in PowerShell when creating Azure Entra ID federated identity credentials, missing OpenSSL utilities for generating cryptographically secure JWT keys, and directory-context sensitivity with GitHub CLI causing `fatal: not a git repository`).

Furthermore, deployed container apps were vulnerable to crash-loop terminations on initial boot if mandatory cryptographic keys (`JWT_KEY`) and AI vision secrets (`GEMINI_API_KEY`) were not provisioned in GitHub environment secrets and mirrored into Azure Key Vault ahead of time.

This ADR records the architectural decision to establish a standardized, 100% native PowerShell pre-deployment configuration runbook and companion automation script:
1. **Universal PowerShell Standard**: All pre-deployment verification, Azure Entra ID federated credential creation, RBAC assignments, and smoke testing use native PowerShell (PowerShell 7 `pwsh` and Windows PowerShell).
2. **Defensive GitHub CLI Scoping**: All `gh` commands explicitly specify `--repo nikunjbanker/diet-dost` to eliminate path-dependent Git errors.
3. **Cryptographic Key Generation**: Replaces external `openssl` utilities with native .NET `System.Security.Cryptography.RandomNumberGenerator` to reliably generate 256-bit (minimum 32-byte) HMAC-SHA256 signing keys on any host.
4. **Temporary JSON Parameter Pattern**: Avoids PowerShell quotation stripping by serializing credential payloads via `@hashtable | ConvertTo-Json -Compress` written to `$env:TEMP` and passed to `az ad app federated-credential create --parameters "@$tempFile"`.
5. **Executable Automation Script**: Codifies the entire pre-flight verification pipeline into [`scripts/setup-azure-pre-deployment.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/setup-azure-pre-deployment.ps1).

---

## 2. Decision Details & Configuration Matrix

### 2.1 Configuration Hierarchy & Pre-Flight Invariants

```
Level 1: Azure Entra ID & Subscription RBAC
  ├── Federated Credential (OIDC): repo:nikunjbanker/diet-dost:environment:dev
  ├── Contributor Role (Infrastructure provisioning)
  ├── Role Based Access Control Administrator Role (Managed Identity Key Vault delegation)
  └── Key Vault Secrets Officer Role (GitHub Actions step 10b Key Vault write access)

Level 2: GitHub Environment 'dev'
  ├── Variables: AZURE_CLIENT_ID, AZURE_SUBSCRIPTION_ID, AZURE_TENANT_ID, SUPER_ADMIN_EMAIL
  └── Secrets: JWT_KEY (≥32 bytes), GEMINI_API_KEY

Level 3: Azure Key Vault (Automatic Sync in Workflow Step 10b)
  ├── Jwt--Key
  └── AI--GoogleAI--ApiKey

Level 4: Azure Container Apps Workload
  ├── Ingress: External on port 8080
  ├── Volume Mount: /app/data -> dietdoststorage (SMB share dietdost-data)
  └── Scaling Guard: minReplicas: 1, maxReplicas: 1 (SQLite single-writer invariant)

Level 5: DNS & Free Managed TLS 1.3
  ├── CNAME: dev -> <app-fqdn>
  ├── TXT: asuid.dev -> <customDomainVerificationId>
  └── Managed Certificate: Free SNI binding for dev.diet-dost.in
```

### 2.2 Reusable PowerShell Automation Patterns

#### 1. Native .NET Cryptographic Key Generation
```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$SecureJwtKey = [Convert]::ToBase64String($bytes)
```

#### 2. Quote-Safe Federated Credential Creation
```powershell
$payload = @{
    name        = "gh-dietdost-dev-env"
    issuer      = "https://token.actions.githubusercontent.com"
    subject     = "repo:nikunjbanker/diet-dost:environment:dev"
    description = "GitHub Actions OIDC trust for dev environment"
    audiences   = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Compress

$tempFile = Join-Path -Path $env:TEMP -ChildPath "fed_cred.json"
[System.IO.File]::WriteAllText($tempFile, $payload)
az ad app federated-credential create --id $AppObjectId --parameters "@$tempFile"
Remove-Item -Path $tempFile -Force
```

---

## 3. Consequences & Verification Impact

### Positive Consequences
- **Zero Friction on Windows**: DevOps operators can execute the pre-deployment steps directly from PowerShell without WSL, bash emulators, or quotation bugs.
- **Fail-Fast Prevention**: Ensures `JWT_KEY` is guaranteed $\ge 32$ bytes before container creation, preventing ASP.NET Core startup crash loops.
- **Repeatability**: The companion script [`scripts/setup-azure-pre-deployment.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/setup-azure-pre-deployment.ps1) allows automated, idempotent setup across `dev`, `staging`, and `prod` environments.
- **Audit & Governance**: Complete alignment with DPDPA 2023 and OWASP secret management standards.

### Verification Strategy
- Executed `scripts/setup-azure-pre-deployment.ps1 -DryRun` to validate script syntax and parameter resolution.
- Validated all links and references in [`docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md).

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Diet-Dost Enterprise Cloud Pre-Deployment & Configuration Guide (PowerShell Edition)

> **Target Architecture**: .NET 11 Aspire WebGateway on Azure Container Apps (ACA)  
> **Persistence**: Azure Files SMB Volume (`/app/data`) with durable SQLite single-replica isolation  
> **Security Perimeter**: OIDC Workload Identity Federation (passwordless), Azure Key Vault RBAC, OWASP Top 10  
> **Scripting Standard**: 100% Native PowerShell (PowerShell 7 / `pwsh` or Windows PowerShell) — Zero Bash Dependencies  
> **Target Domain**: `dev.diet-dost.in` with Free Azure-Managed TLS 1.3 Certificate  

---

## Architecture & Configuration Topology

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ LEVEL 1: Azure Entra ID & Subscription (Passwordless OIDC Trust & RBAC Roles)          │
│ - App Registration: Service Principal (SP) Object & Client ID                          │
│ - Federated Credential: repo:nikunjbanker/diet-dost:environment:dev                    │
│ - Roles: Contributor, Role Based Access Control Administrator, Key Vault Secrets Officer│
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ LEVEL 2: GitHub Repository & Environment Configuration (`dev` environment)             │
│ - Variables: AZURE_CLIENT_ID, AZURE_SUBSCRIPTION_ID, AZURE_TENANT_ID                  │
│ - Secrets: JWT_KEY (Cryptographic 256-bit HMAC), GEMINI_API_KEY (AI Vision)           │
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ LEVEL 3: Azure Infrastructure as Code (Bicep Engine via GitHub Actions)               │
│ - Virtual Network (`vnet-dietdost-infra`) + Delegated Subnet (`snet-aca-infra`)        │
│ - Azure Container Apps Managed Environment (`cae-dietdost-dev`)                        │
│ - Azure Container Registry (`crdietdost<hash>`) Basic SKU                              │
│ - Azure Storage Account (`stdietdost<hash>`) + Azure Files SMB Share (`dietdost-data`) │
│ - Azure Key Vault (`kvdietdost<hash>`) with Managed Identity RBAC access               │
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ LEVEL 4: Workload Deployment & Volume Mount Configuration                              │
│ - Container: app-dietdost-web (Linux Chiseled Non-Root, Port 8080)                    │
│ - SMB Storage Mount: /app/data -> dietdoststorage (diet_dost.db durability)            │
│ - Golden Rule SQLite Constraint: minReplicas = 1, maxReplicas = 1                      │
└──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                           │
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ LEVEL 5: Domain DNS Resolution & Free Managed TLS 1.3 Certificate                      │
│ - DNS CNAME: dev -> app-dietdost-web.<unique>.centralindia.azurecontainerapps.io       │
│ - DNS TXT: asuid.dev -> <customDomainVerificationId>                                   │
│ - Azure Managed Certificate: Free SNI binding for dev.diet-dost.in                    │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 1. Master Configuration Inventory (What, Why, and How)

| Level | Key / Setting Name | Value Type / Example | Mandatory? | What It Is & Why It Is Mandatory | How to Configure |
| :--- | :--- | :--- | :---: | :--- | :--- |
| **Level 1**<br/>(Entra ID) | **Federated Credential (OIDC)** | `repo:nikunjbanker/diet-dost:environment:dev` | **CRITICAL** | **What**: Trust relationship between GitHub Actions and Entra ID.<br/>**Why**: Enables passwordless authentication. Without this, `azure/login@v2` aborts with `AADSTS700212`. | Executed in Phase 1 via `az ad app federated-credential create`. |
| **Level 1**<br/>(Subscription) | **Contributor** | Azure RBAC Role | **CRITICAL** | **What**: Subscription-level resource provisioning role.<br/>**Why**: Allows the pipeline to provision Resource Groups, Container Apps, Key Vault, and Storage Accounts. | Executed in Phase 1 via `az role assignment create --role Contributor`. |
| **Level 1**<br/>(Subscription) | **Role Based Access Control Administrator** | Azure RBAC Role | **CRITICAL** | **What**: Permission to grant Azure RBAC roles.<br/>**Why**: Bicep assigns `Key Vault Secrets User` to the Container App's User-Assigned Managed Identity. Without this, deployment halts with `AuthorizationFailed`. | Executed in Phase 1 via `az role assignment create --role "Role Based Access Control Administrator"`. |
| **Level 1**<br/>(Subscription) | **Key Vault Secrets Officer** | Azure RBAC Role | **CRITICAL** | **What**: Permission to read and write secrets in Azure Key Vault.<br/>**Why**: Allows Step 10b of deployment workflow to push `Jwt--Key` and `AI--GoogleAI--ApiKey` into Key Vault. | Executed in Phase 1 via `az role assignment create --role "Key Vault Secrets Officer"`. |
| **Level 2**<br/>(GitHub Env) | `AZURE_CLIENT_ID` | GUID (`<app-client-id>`) | **YES** | **What**: Service Principal Application Client ID.<br/>**Why**: Required by `azure/login@v2` to identify the workload identity. | Dynamically retrieved via `az ad app list` or GitHub Env `dev`. |
| **Level 2**<br/>(GitHub Env) | `AZURE_SUBSCRIPTION_ID` | GUID (`<subscription-id>`) | **YES** | **What**: Azure Subscription ID.<br/>**Why**: Directs Azure CLI and ARM deployment to the target subscription. | Dynamically retrieved via `az account show` or GitHub Env `dev`. |
| **Level 2**<br/>(GitHub Env) | `AZURE_TENANT_ID` | GUID (`<tenant-id>`) | **YES** | **What**: Microsoft Entra Tenant ID.<br/>**Why**: Identifies the directory for authentication token issuance. | Dynamically retrieved via `az account show` or GitHub Env `dev`. |
| **Level 2**<br/>(GitHub Secret) | `JWT_KEY` | $\ge 32$-byte cryptographic key | **CRITICAL** | **What**: HMAC-SHA256 signing secret for authentication tokens.<br/>**Why**: In Production/Staging, the app enforces strict startup validation and **terminates the container immediately** if missing or $< 32$ bytes. | Generated via PowerShell .NET CSP and saved via `gh secret set`. |
| **Level 2**<br/>(GitHub Secret) | `GEMINI_API_KEY` | `AIzaSy...` (Google API Key) | **YES** | **What**: Google AI Gemini API secret.<br/>**Why**: Powers AI food recognition, macro breakdown, and Indian dish estimation. | Saved via `gh secret set GEMINI_API_KEY`. |
| **Level 3**<br/>(Key Vault) | `Jwt--Key` | Key Vault Secret | **AUTOMATIC** | **What**: Mirrored secret pulled at runtime by .NET configuration provider.<br/>**Why**: Keeps credentials out of code and container environment variables. | Synced automatically by pipeline Step 10b. |
| **Level 3**<br/>(Key Vault) | `AI--GoogleAI--ApiKey` | Key Vault Secret | **AUTOMATIC** | **What**: Mirrored secret pulled at runtime for `AiOptions`.<br/>**Why**: Centralized secret governance. | Synced automatically by pipeline Step 10b. |
| **Level 4**<br/>(Container App) | Volume `/app/data` &rarr; `dietdoststorage` | Azure Files SMB Mount | **AUTOMATIC** | **What**: Durable persistent volume attached to Azure Files SMB share.<br/>**Why**: Protects SQLite database (`diet_dost.db`) from wiping upon container restart or scale revision. | Provisioned automatically by `infra/app.bicep`. |
| **Level 4**<br/>(Container App) | `minReplicas: 1, maxReplicas: 1` | Scale bounds | **AUTOMATIC** | **What**: Strict single-replica boundary.<br/>**Why**: Prevents concurrent process write collisions and database corruption on SQLite files. | Hardcoded in `infra/app.bicep`. |
| **Level 5**<br/>(Domain DNS) | `CNAME dev` & `TXT asuid.dev` | DNS records | *Optional* | **What**: DNS routing and domain ownership proof.<br/>**Why**: Required by Azure Container Apps to issue and bind a **free managed TLS certificate** for `dev.diet-dost.in`. | Configured at Domain Registrar (Cloudflare, GoDaddy, etc.). |

---

## 2. Step-by-Step Pre-Deployment Execution Guide (PowerShell)

### Phase 0: Initialize Common PowerShell Session Variables

Open your PowerShell terminal (PowerShell 7 `pwsh` or Windows PowerShell) and execute this block once.  
> [!NOTE]
> All cloud IDs (Subscription ID, Tenant ID, App Registration ID) are **dynamically resolved from your active Azure session**. Only authorized users authenticated via `az login` can retrieve these identifiers.

```powershell
# ==============================================================================
# Phase 0: Common Session Variables & Dynamic Azure Cloud Context
# ==============================================================================
$ErrorActionPreference = "Stop"

# 1. Target Repository & Branch
$GitHubRepo        = "nikunjbanker/diet-dost"
$EnvironmentName   = "dev"
$GitBranch         = "feature/aca-deployment-sqlite-smb-dev-domain"

# 2. Azure Target Resource Group & Location
$ResourceGroup     = "rg-dietdost-dev"
$Location          = "centralindia"

# 3. Authenticate Azure CLI
Write-Host "Authenticating Azure CLI..." -ForegroundColor Cyan
az login
$SubscriptionId    = az account show --query "id" -o tsv
$TenantId          = az account show --query "tenantId" -o tsv
az account set --subscription $SubscriptionId

# 4. Dynamically Resolve Entra ID App Registration Client ID
$AppRegistrationId = az ad app list --display-name "app-dietdost-web" --query "[0].appId" -o tsv
if (-not $AppRegistrationId) {
    $AppRegistrationId = az ad sp list --display-name "app-dietdost-web" --query "[0].appId" -o tsv
}

Write-Host "✅ Active Subscription : $(az account show --query 'name' -o tsv) ($SubscriptionId)" -ForegroundColor Green
Write-Host "✅ Active Tenant ID    : $TenantId" -ForegroundColor Green
Write-Host "✅ App Registration ID : $AppRegistrationId" -ForegroundColor Green
```

---

### Phase 1: Azure Entra ID OIDC & Subscription RBAC Permissions (Level 1)

Execute these steps in PowerShell to establish passwordless OIDC trust and assign mandatory RBAC roles:

```powershell
# 1. Retrieve Service Principal Application Object ID dynamically
$AppObjectId = az ad app show --id $AppRegistrationId --query "id" -o tsv
Write-Host "App Object ID: $AppObjectId" -ForegroundColor Cyan

# 3. Create OIDC Federated Identity Credential for GitHub Environment 'dev'
# (Using a temporary JSON file to avoid PowerShell quotation stripping)
$fedCredDev = @{
    name        = "gh-dietdost-dev-env"
    issuer      = "https://token.actions.githubusercontent.com"
    subject     = "repo:${GitHubRepo}:environment:${EnvironmentName}"
    description = "GitHub Actions OIDC trust for dev environment"
    audiences   = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Compress

$tempFileDev = Join-Path -Path $env:TEMP -ChildPath "fed_cred_dev.json"
[System.IO.File]::WriteAllText($tempFileDev, $fedCredDev)

Write-Host "Creating Federated Credential for GitHub 'dev' environment..." -ForegroundColor Cyan
az ad app federated-credential create `
  --id $AppObjectId `
  --parameters "@$tempFileDev" `
  2>$null || Write-Host "ℹ️ Federated credential 'gh-dietdost-dev-env' already exists." -ForegroundColor Yellow

Remove-Item -Path $tempFileDev -Force -ErrorAction SilentlyContinue

# 4. Create OIDC Federated Identity Credential for the Feature Branch (Fallback)
$fedCredBranch = @{
    name        = "gh-dietdost-branch-cred"
    issuer      = "https://token.actions.githubusercontent.com"
    subject     = "repo:${GitHubRepo}:ref:refs/heads/${GitBranch}"
    description = "GitHub Actions OIDC trust for feature branch"
    audiences   = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Compress

$tempFileBranch = Join-Path -Path $env:TEMP -ChildPath "fed_cred_branch.json"
[System.IO.File]::WriteAllText($tempFileBranch, $fedCredBranch)

Write-Host "Creating Federated Credential for feature branch..." -ForegroundColor Cyan
az ad app federated-credential create `
  --id $AppObjectId `
  --parameters "@$tempFileBranch" `
  2>$null || Write-Host "ℹ️ Federated credential 'gh-dietdost-branch-cred' already exists." -ForegroundColor Yellow

Remove-Item -Path $tempFileBranch -Force -ErrorAction SilentlyContinue

# 5. Assign Subscription-Level Roles to Service Principal
$Scope = "/subscriptions/$SubscriptionId"

Write-Host "Granting 'Contributor' role..." -ForegroundColor Cyan
az role assignment create --assignee $AppRegistrationId --role "Contributor" --scope $Scope 2>$null || Write-Host "ℹ️ Contributor role already assigned." -ForegroundColor Yellow

Write-Host "Granting 'Role Based Access Control Administrator' role..." -ForegroundColor Cyan
az role assignment create --assignee $AppRegistrationId --role "Role Based Access Control Administrator" --scope $Scope 2>$null || Write-Host "ℹ️ RBAC Administrator role already assigned." -ForegroundColor Yellow

Write-Host "Granting 'Key Vault Secrets Officer' role..." -ForegroundColor Cyan
az role assignment create --assignee $AppRegistrationId --role "Key Vault Secrets Officer" --scope $Scope 2>$null || Write-Host "ℹ️ Key Vault Secrets Officer role already assigned." -ForegroundColor Yellow

Write-Host "🎉 Phase 1 Complete: Azure Entra ID & RBAC Roles configured successfully!" -ForegroundColor Green
```

---

### Phase 2: GitHub Environment Variables & Mandatory Secrets Setup (Level 2)

Execute these steps in PowerShell to configure required environment variables and secrets:

```powershell
# 1. Verify GitHub CLI Authentication
gh auth status

# 2. Set / Confirm GitHub Environment Variables
Write-Host "Configuring GitHub Environment Variables for '$EnvironmentName'..." -ForegroundColor Cyan
gh variable set AZURE_CLIENT_ID --repo $GitHubRepo --env $EnvironmentName --body $AppRegistrationId
gh variable set AZURE_SUBSCRIPTION_ID --repo $GitHubRepo --env $EnvironmentName --body $SubscriptionId
gh variable set AZURE_TENANT_ID --repo $GitHubRepo --env $EnvironmentName --body $TenantId
gh variable set SUPER_ADMIN_EMAIL --repo $GitHubRepo --env $EnvironmentName --body "superadmin@dietdost.app"
gh variable set REQUIRE_MOBILE_VERIFICATION --repo $GitHubRepo --env $EnvironmentName --body "false"

# 3. Generate Cryptographically Secure 256-bit (48-byte) JWT Signing Key
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$SecureJwtKey = [Convert]::ToBase64String($bytes)
Write-Host "Generated 256-bit JWT Key: $SecureJwtKey" -ForegroundColor Yellow

# 4. Save JWT_KEY Secret to GitHub Environment
gh secret set JWT_KEY --repo $GitHubRepo --env $EnvironmentName --body $SecureJwtKey
Write-Host "✅ JWT_KEY successfully saved in GitHub environment '$EnvironmentName'!" -ForegroundColor Green

# 5. Save GEMINI_API_KEY Secret to GitHub Environment
# Replace with your actual Google Gemini API key:
$MyGeminiApiKey = "YOUR_ACTUAL_GEMINI_API_KEY_HERE"

if ($MyGeminiApiKey -ne "YOUR_ACTUAL_GEMINI_API_KEY_HERE") {
    gh secret set GEMINI_API_KEY --repo $GitHubRepo --env $EnvironmentName --body $MyGeminiApiKey
    Write-Host "✅ GEMINI_API_KEY saved in GitHub environment '$EnvironmentName'!" -ForegroundColor Green
} else {
    Write-Host "⚠️ Please update `$MyGeminiApiKey with your actual key and run: gh secret set GEMINI_API_KEY --repo $GitHubRepo --env $EnvironmentName --body <key>" -ForegroundColor Magenta
}

# 6. Verify Configured Environment Secrets & Variables
Write-Host "--- Configured Environment Variables ---" -ForegroundColor Cyan
gh variable list --repo $GitHubRepo --env $EnvironmentName

Write-Host "--- Configured Environment Secrets ---" -ForegroundColor Cyan
gh secret list --repo $GitHubRepo --env $EnvironmentName

Write-Host "🎉 Phase 2 Complete: GitHub Environment Variables & Secrets ready!" -ForegroundColor Green
```

---

### Phase 3: Trigger the Aspire Deployment Workflow (Level 3 & 4)

Now trigger the Aspire Deployment Workflow directly from PowerShell.  
> [!IMPORTANT]
> For the initial deployment run, keep `enableCustomDomain="false"` because Azure generates the unique Container App FQDN and verification token during this first deployment.

```powershell
Write-Host "🚀 Dispatching Aspire Build & Deploy Pipeline on branch '$GitBranch'..." -ForegroundColor Cyan

gh workflow run "azure-app-deploy.yml" `
  --repo $GitHubRepo `
  --ref $GitBranch `
  -f resourceGroupName=$ResourceGroup `
  -f location=$Location `
  -f provisionInfra="true" `
  -f allowDemoUsers="true" `
  -f enableCustomDomain="false" `
  -f environment=$EnvironmentName

Write-Host "✅ Pipeline dispatched successfully!" -ForegroundColor Green
Write-Host "Tracking latest workflow run..." -ForegroundColor Cyan

Start-Sleep -Seconds 5
$runId = gh run list --repo $GitHubRepo --workflow="azure-app-deploy.yml" --limit 1 --json databaseId --jq ".[0].databaseId"
Write-Host "Monitoring Run ID: $runId" -ForegroundColor Yellow

# Watch live output in PowerShell:
gh run watch $runId --repo $GitHubRepo
```

---

### Phase 4: Configure DNS & Bind Custom Domain (`dev.diet-dost.in`) (Level 5)

Once the Phase 3 deployment completes successfully, retrieve the default FQDN and verification token:

```powershell
# 1. Retrieve the Container App FQDN and Domain Verification ID
$AcaFqdn = az containerapp show `
  --name "app-dietdost-web" `
  --resource-group $ResourceGroup `
  --query "properties.configuration.ingress.fqdn" -o tsv

$VerificationId = az containerapp env show `
  --name "cae-dietdost-$EnvironmentName" `
  --resource-group $ResourceGroup `
  --query "properties.customDomainConfiguration.customDomainVerificationId" -o tsv

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ACA INGRESS FQDN   : $AcaFqdn" -ForegroundColor Yellow
Write-Host "  VERIFICATION TOKEN : $VerificationId" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
```

#### 2. Add DNS Records at Your Registrar
Log into your DNS provider (Cloudflare, GoDaddy, Namecheap, etc.) and add these **two records**:

| Type | Host / Name | Value / Destination | TTL | Purpose |
| :--- | :--- | :--- | :---: | :--- |
| **CNAME** | `dev` | `$AcaFqdn` *(e.g. `app-dietdost-web.happyrock-xxxx.centralindia.azurecontainerapps.io`)* | 300 | Routes traffic to Azure Container App |
| **TXT** | `asuid.dev` | `$VerificationId` *(e.g. `B6F49C12...`)* | 300 | Proves domain ownership to Azure |

#### 3. Verify DNS Propagation in PowerShell
```powershell
Write-Host "Checking DNS propagation..." -ForegroundColor Cyan
Resolve-DnsName -Name "dev.diet-dost.in" -Type CNAME
Resolve-DnsName -Name "asuid.dev.diet-dost.in" -Type TXT
```

#### 4. Bind Custom Domain & Provision Free Managed TLS Certificate
Once both DNS records resolve:
```powershell
$CustomDomain = "dev.diet-dost.in"
$EnvironmentResource = "cae-dietdost-$EnvironmentName"

Write-Host "Adding custom hostname '$CustomDomain' to Container App..." -ForegroundColor Cyan
az containerapp hostname add `
  --name "app-dietdost-web" `
  --resource-group $ResourceGroup `
  --hostname $CustomDomain

Write-Host "Provisioning Free Azure-Managed TLS Certificate and binding SNI..." -ForegroundColor Cyan
az containerapp hostname bind `
  --name "app-dietdost-web" `
  --resource-group $ResourceGroup `
  --hostname $CustomDomain `
  --environment $EnvironmentResource `
  --validation-method CNAME

Write-Host "🎉 Custom Domain '$CustomDomain' successfully bound with Free Azure Managed TLS 1.3!" -ForegroundColor Green
```

---

### Phase 5: Post-Deployment Smoke Test & Health Check (PowerShell)

Verify the operational health, authentication gate, and data persistence of the deployed application:

```powershell
# ==============================================================================
# Phase 5: Verification Suite
# ==============================================================================
$TargetUrl = "https://$AcaFqdn"
# Once custom domain is bound: $TargetUrl = "https://dev.diet-dost.in"

Write-Host "Executing smoke tests against: $TargetUrl" -ForegroundColor Cyan

# 1. Health Probe Checks
$healthProbe = Invoke-RestMethod -Uri "$TargetUrl/healthz" -Method Get
Write-Host "1. /healthz Status: $healthProbe" -ForegroundColor Green

$readyProbe = Invoke-RestMethod -Uri "$TargetUrl/ready" -Method Get
Write-Host "2. /ready Status: $readyProbe" -ForegroundColor Green

# 2. Demo User Authentication Test (Free Tier)
$loginBody = @{
    email    = "free@dietdost.app"
    password = "DietDost@Demo2026!"
} | ConvertTo-Json

$authResponse = Invoke-RestMethod `
  -Uri "$TargetUrl/api/auth/login" `
  -Method Post `
  -ContentType "application/json" `
  -Body $loginBody

Write-Host "3. Authentication Test: User '$($authResponse.user.email)' authenticated successfully (Tier: $($authResponse.user.tier))" -ForegroundColor Green

# 3. Web BFF Dashboard Composite Hydration Test
$token = $authResponse.token
$dashboard = Invoke-RestMethod `
  -Uri "$TargetUrl/api/web/v1/dashboard" `
  -Method Get `
  -Headers @{ Authorization = "Bearer $token" }

Write-Host "4. Web BFF Dashboard: Target Calories = $($dashboard.profile.targetCalories) kcal, Total Meals = $($dashboard.diary.totalMeals)" -ForegroundColor Green

# 4. Verify SQLite SMB Durability across Container Restarts
Write-Host "5. Testing SQLite Database Persistence..." -ForegroundColor Cyan
Write-Host "   Restarting Container App Revision..." -ForegroundColor Yellow
az containerapp revision restart --name "app-dietdost-web" --resource-group $ResourceGroup --revision $(az containerapp revision list --name "app-dietdost-web" --resource-group $ResourceGroup --query "[0].name" -o tsv)

Start-Sleep -Seconds 15

# Re-query dashboard after restart
$dashboardAfterRestart = Invoke-RestMethod `
  -Uri "$TargetUrl/api/web/v1/dashboard" `
  -Method Get `
  -Headers @{ Authorization = "Bearer $token" }

Write-Host "   Verification: Dashboard successfully hydrated after container restart! Total Meals = $($dashboardAfterRestart.diary.totalMeals)" -ForegroundColor Green

Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "🚀 DIET-DOST ENTERPRISE CLOUD APPLICATION IS 100% OPERATIONAL IN PRODUCTION! " -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
```

---

## 3. Troubleshooting & Common Pitfalls

| Symptom | Root Cause | PowerShell Resolution |
| :--- | :--- | :--- |
| `fatal: not a git repository` when running `gh` | Terminal is open in a directory outside the Git workspace. | Use `--repo nikunjbanker/diet-dost` in all `gh` commands, or run `Set-Location c:\Users\nikunj.banker\source\repos\diet-dost`. |
| `AADSTS700212: Invalid subject` during GitHub Actions Azure login | Federated Credential subject mismatch. | Run Step 1.3 to ensure subject is exactly `repo:nikunjbanker/diet-dost:environment:dev`. |
| `AuthorizationFailed: ... do not have authorization to perform action 'Microsoft.Authorization/roleAssignments/write'` | Service Principal lacks permission to grant Managed Identity Key Vault access. | Run Step 1.5 to assign `Role Based Access Control Administrator` on the subscription scope. |
| Container App fails immediately with exit code 1 or crash loop | Missing or short `JWT_KEY`. | Run Step 2.4 to generate and save a 256-bit `JWT_KEY` secret. In non-development mode, .NET startup fails fast if this is $< 32$ bytes. |
| `Conflict: The custom domain is already bound` | Previous binding attempt left a partial record. | Run `az containerapp hostname delete --name app-dietdost-web --resource-group rg-dietdost-dev --hostname dev.diet-dost.in` then re-bind. |

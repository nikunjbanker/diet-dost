# ==============================================================================
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
# ==============================================================================
# Script: setup-azure-pre-deployment.ps1
# Purpose: Interactive & Automated Pre-Deployment Configuration for Diet-Dost
# Target Environment: Azure Container Apps + Persistent SQLite SMB + dev.diet-dost.in
# ==============================================================================

[CmdletBinding(SupportsShouldProcess = $true)]
param (
    [string]$SubscriptionId    = "",
    [string]$TenantId          = "",
    [string]$AppRegistrationId = "",
    [string]$GitHubRepo        = "nikunjbanker/diet-dost",
    [string]$EnvironmentName   = "dev",
    [string]$GitBranch         = "feature/aca-deployment-sqlite-smb-dev-domain",
    [string]$GeminiApiKey      = "",
    [switch]$SkipAzLogin,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "      DIET-DOST ENTERPRISE CLOUD PRE-DEPLOYMENT SETUP (POWERSHELL)           " -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "Repository   : $GitHubRepo" -ForegroundColor Yellow
Write-Host "Environment  : $EnvironmentName" -ForegroundColor Yellow
Write-Host "Branch       : $GitBranch" -ForegroundColor Yellow
Write-Host "------------------------------------------------------------------------------"

# ------------------------------------------------------------------------------
# 1. Prerequisite Tool Verification
# ------------------------------------------------------------------------------
Write-Host "`n[1/5] Verifying Prerequisite CLI Tools..." -ForegroundColor Cyan

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI ('az') is not installed or not in PATH. Please install Azure CLI."
}
$azVer = (az version -o json 2>$null | ConvertFrom-Json).'azure-cli'
Write-Host "  ✅ Azure CLI detected: $azVer" -ForegroundColor Green

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI ('gh') is not installed or not in PATH. Please install GitHub CLI."
}
Write-Host "  ✅ GitHub CLI detected." -ForegroundColor Green

# ------------------------------------------------------------------------------
# 2. Azure CLI Authentication & Dynamic Context Resolution
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Checking Azure Authentication & Resolving Cloud Context..." -ForegroundColor Cyan

if (-not $SkipAzLogin) {
    $currentAccount = az account show --query "id" -o tsv 2>$null
    if (-not $currentAccount) {
        Write-Host "  Logging in to Azure..." -ForegroundColor Yellow
        az login
    }
}

# Dynamically resolve active subscription and tenant from authenticated session
if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = (az account show --query "id" -o tsv)
}
if ([string]::IsNullOrWhiteSpace($TenantId)) {
    $TenantId = (az account show --query "tenantId" -o tsv)
}

az account set --subscription $SubscriptionId
$activeSubName = az account show --query "name" -o tsv
Write-Host "  ✅ Azure Subscription dynamically resolved: $activeSubName ($SubscriptionId)" -ForegroundColor Green
Write-Host "  ✅ Azure Tenant ID dynamically resolved: $TenantId" -ForegroundColor Green

# Dynamically resolve App Registration Client ID from authenticated session
if ([string]::IsNullOrWhiteSpace($AppRegistrationId)) {
    Write-Host "  Dynamically querying Entra ID for deployment App Registration..." -ForegroundColor Yellow
    if ($env:AZURE_CLIENT_ID) {
        $AppRegistrationId = $env:AZURE_CLIENT_ID
    } else {
        $AppRegistrationId = (az ad app list --all --query "[?contains(displayName, 'dietdost')].appId" -o tsv 2>$null | Select-Object -First 1)
        if (-not $AppRegistrationId) {
            $AppRegistrationId = (az ad sp list --all --query "[?contains(displayName, 'dietdost')].appId" -o tsv 2>$null | Select-Object -First 1)
        }
    }
}

if ([string]::IsNullOrWhiteSpace($AppRegistrationId)) {
    throw "Unable to dynamically resolve AppRegistrationId from active Azure session. Please run 'az login' with an authorized account or pass -AppRegistrationId <id>."
}
Write-Host "  ✅ App Registration Client ID dynamically resolved: $AppRegistrationId" -ForegroundColor Green

# ------------------------------------------------------------------------------
# 3. Entra ID App Federated Credentials & Subscription RBAC Roles
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Configuring Entra ID Federated Credentials & Subscription RBAC..." -ForegroundColor Cyan

$AppObjectId = az ad app show --id $AppRegistrationId --query "id" -o tsv
Write-Host "  Service Principal App Object ID: $AppObjectId" -ForegroundColor Yellow

# Helper function to create federated credential safely
function Set-FederatedCredential {
    param (
        [string]$Name,
        [string]$Subject,
        [string]$Description
    )
    $payload = @{
        name        = $Name
        issuer      = "https://token.actions.githubusercontent.com"
        subject     = $Subject
        description = $Description
        audiences   = @("api://AzureADTokenExchange")
    } | ConvertTo-Json -Compress

    $tempFile = Join-Path -Path $env:TEMP -ChildPath "fed_cred_$Name.json"
    [System.IO.File]::WriteAllText($tempFile, $payload)

    if ($DryRun) {
        Write-Host "  [DryRun] Would create federated credential '$Name' for subject '$Subject'" -ForegroundColor Magenta
    } else {
        az ad app federated-credential create --id $AppObjectId --parameters "@$tempFile" 2>$null
        Write-Host "  ✅ Federated credential '$Name' verified." -ForegroundColor Green
    }
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
}

# 3a. Environment credential
Set-FederatedCredential `
    -Name "gh-dietdost-dev-env" `
    -Subject "repo:${GitHubRepo}:environment:${EnvironmentName}" `
    -Description "GitHub Actions OIDC trust for $EnvironmentName environment"

# 3b. Branch credential (fallback)
Set-FederatedCredential `
    -Name "gh-dietdost-branch-cred" `
    -Subject "repo:${GitHubRepo}:ref:refs/heads/${GitBranch}" `
    -Description "GitHub Actions OIDC trust for branch $GitBranch"

# 3c. Subscription RBAC Role Assignments
$Scope = "/subscriptions/$SubscriptionId"
$roles = @("Contributor", "Role Based Access Control Administrator", "Key Vault Secrets Officer")

foreach ($role in $roles) {
    if ($DryRun) {
        Write-Host "  [DryRun] Would assign role '$role' on scope $Scope" -ForegroundColor Magenta
    } else {
        Write-Host "  Verifying role '$role'..." -ForegroundColor Yellow
        az role assignment create --assignee $AppRegistrationId --role $role --scope $Scope 2>$null
        Write-Host "  ✅ Role '$role' confirmed." -ForegroundColor Green
    }
}

# ------------------------------------------------------------------------------
# 4. GitHub Environment Variables & Secrets Configuration
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Configuring GitHub Environment Variables & Secrets..." -ForegroundColor Cyan

# 4a. Environment Variables
$envVars = @{
    "AZURE_CLIENT_ID"             = $AppRegistrationId
    "AZURE_SUBSCRIPTION_ID"        = $SubscriptionId
    "AZURE_TENANT_ID"              = $TenantId
    "SUPER_ADMIN_EMAIL"           = "superadmin@dietdost.app"
    "REQUIRE_MOBILE_VERIFICATION" = "false"
}

foreach ($kv in $envVars.GetEnumerator()) {
    if ($DryRun) {
        Write-Host "  [DryRun] Would set GitHub variable '$($kv.Key)' to '$($kv.Value)'" -ForegroundColor Magenta
    } else {
        gh variable set $kv.Key --repo $GitHubRepo --env $EnvironmentName --body $kv.Value
        Write-Host "  ✅ GitHub Variable '$($kv.Key)' set." -ForegroundColor Green
    }
}

# 4b. Cryptographic JWT Signing Key
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$SecureJwtKey = [Convert]::ToBase64String($bytes)

if ($DryRun) {
    Write-Host "  [DryRun] Would set 256-bit JWT_KEY secret" -ForegroundColor Magenta
} else {
    gh secret set JWT_KEY --repo $GitHubRepo --env $EnvironmentName --body $SecureJwtKey
    Write-Host "  ✅ Cryptographic JWT_KEY (256-bit HMAC) generated and stored." -ForegroundColor Green
}

# 4c. Gemini API Key
if ([string]::IsNullOrWhiteSpace($GeminiApiKey)) {
    Write-Host "  ℹ️ Note: GEMINI_API_KEY was not passed via parameter." -ForegroundColor Yellow
    Write-Host "     To set it manually, execute:" -ForegroundColor Yellow
    Write-Host "     gh secret set GEMINI_API_KEY --repo $GitHubRepo --env $EnvironmentName --body '<your-key>'" -ForegroundColor Cyan
} else {
    if ($DryRun) {
        Write-Host "  [DryRun] Would set GEMINI_API_KEY secret" -ForegroundColor Magenta
    } else {
        gh secret set GEMINI_API_KEY --repo $GitHubRepo --env $EnvironmentName --body $GeminiApiKey
        Write-Host "  ✅ GEMINI_API_KEY secret stored in environment '$EnvironmentName'." -ForegroundColor Green
    }
}

# ------------------------------------------------------------------------------
# 5. Summary & Next Steps
# ------------------------------------------------------------------------------
Write-Host "`n[5/5] Verification Summary" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "🎉 PRE-DEPLOYMENT PREREQUISITES ARE FULLY CONFIGURED!" -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "You are now ready to trigger the deployment pipeline in GitHub Actions:" -ForegroundColor Yellow
Write-Host @"
gh workflow run 'azure-app-deploy.yml' `
  --repo $GitHubRepo `
  --ref $GitBranch `
  -f resourceGroupName=rg-dietdost-$EnvironmentName `
  -f location=centralindia `
  -f provisionInfra=true `
  -f allowDemoUsers=true `
  -f enableCustomDomain=false `
  -f environment=$EnvironmentName
"@ -ForegroundColor White
Write-Host "==============================================================================" -ForegroundColor Green

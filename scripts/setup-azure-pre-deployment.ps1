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
    [string]$SubscriptionId            = "",
    [string]$TenantId                  = "",
    [string]$AppRegistrationName       = "sp-dev-dietdost-github-deployer",
    [string]$AppRegistrationId         = "",
    [string]$GitHubRepo                = "nikunjbanker/diet-dost",
    [string]$EnvironmentName           = "dev",
    [string]$GitBranch                 = "",
    [string]$SuperAdminEmail           = "superadmin@dietdost.app",
    [string]$RequireMobileVerification = "false",
    [string]$GeminiApiKey              = "",
    [string]$AzureOpenAiApiKey         = "",
    [switch]$SkipAzLogin,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Dynamically resolve active Git branch if not explicitly specified
if ([string]::IsNullOrWhiteSpace($GitBranch)) {
    $GitBranch = (git branch --show-current 2>$null)
    if ([string]::IsNullOrWhiteSpace($GitBranch)) {
        $GitBranch = "main"
    }
}

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
    Write-Host "  Dynamically querying Entra ID for deployment App Registration '$AppRegistrationName'..." -ForegroundColor Yellow
    if ($env:AZURE_CLIENT_ID) {
        $AppRegistrationId = $env:AZURE_CLIENT_ID
    } else {
        $AppRegistrationId = (az ad app list --display-name $AppRegistrationName --query "[0].appId" -o tsv 2>$null)
        if (-not $AppRegistrationId) {
            $AppRegistrationId = (az ad sp list --display-name $AppRegistrationName --query "[0].appId" -o tsv 2>$null)
        }
        if (-not $AppRegistrationId) {
            $AppRegistrationId = (az ad app list --all --query "[?contains(displayName, '$AppRegistrationName')].appId" -o tsv 2>$null | Select-Object -First 1)
        }
        if (-not $AppRegistrationId) {
            $AppRegistrationId = (az ad app list --all --query "[?contains(displayName, 'dietdost')].appId" -o tsv 2>$null | Select-Object -First 1)
        }
    }
}

if ([string]::IsNullOrWhiteSpace($AppRegistrationId)) {
    throw "Unable to dynamically resolve AppRegistrationId from active Azure session. Please run 'az login' with an authorized account or pass -AppRegistrationId <id>."
}
Write-Host "  ✅ App Registration Client ID dynamically resolved: $AppRegistrationId" -ForegroundColor Green

# ------------------------------------------------------------------------------
# ------------------------------------------------------------------------------
# 3. Entra ID App Federated Credentials & Subscription RBAC Roles
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Configuring Entra ID Federated Credentials & Subscription RBAC..." -ForegroundColor Cyan

$AppObjectId = az ad app show --id $AppRegistrationId --query "id" -o tsv
Write-Host "  Service Principal App Object ID: $AppObjectId" -ForegroundColor Yellow

# Query existing federated credentials from Entra ID
Write-Host "  Querying existing federated identity credentials for '$AppRegistrationName'..." -ForegroundColor Yellow
$existingCredsJson = az ad app federated-credential list --id $AppObjectId -o json 2>$null
$existingCreds = if ($existingCredsJson) { $existingCredsJson | ConvertFrom-Json } else { @() }

if ($DryRun) {
    Write-Host "`n  --- [DryRun] Existing Entra ID Federated Credentials ($($existingCreds.Count) found) ---" -ForegroundColor Cyan
    if ($existingCreds.Count -gt 0) {
        foreach ($c in $existingCreds) {
            Write-Host "    • $($c.name)" -ForegroundColor White
            Write-Host "      Subject : $($c.subject)" -ForegroundColor Gray
            Write-Host "      Issuer  : $($c.issuer)" -ForegroundColor Gray
        }
    } else {
        Write-Host "    (No federated credentials currently configured on this App Registration)" -ForegroundColor Gray
    }
    Write-Host ""
}

# Helper function to create/verify federated credential safely
function Set-FederatedCredential {
    param (
        [string]$Name,
        [string]$Subject,
        [string]$Description
    )

    $match = $existingCreds | Where-Object { $_.name -eq $Name }

    if ($match) {
        if ($DryRun) {
            Write-Host "  [DryRun] ✅ Federated credential '$Name' ALREADY EXISTS" -ForegroundColor Green
            Write-Host "           Current Subject: $($match.subject)" -ForegroundColor Gray
        } else {
            Write-Host "  ℹ️ Federated credential '$Name' already exists (Subject: $($match.subject))." -ForegroundColor Yellow
        }
        return
    }

    if ($DryRun) {
        Write-Host "  [DryRun] ⚡ Federated credential '$Name' NOT FOUND (would create for subject: '$Subject')" -ForegroundColor Magenta
        return
    }

    $payload = @{
        name        = $Name
        issuer      = "https://token.actions.githubusercontent.com"
        subject     = $Subject
        description = $Description
        audiences   = @("api://AzureADTokenExchange")
    } | ConvertTo-Json -Compress

    $tempFile = Join-Path -Path $env:TEMP -ChildPath "fed_cred_$Name.json"
    [System.IO.File]::WriteAllText($tempFile, $payload)

    az ad app federated-credential create --id $AppObjectId --parameters "@$tempFile" 2>$null
    Write-Host "  ✅ Federated credential '$Name' verified/created." -ForegroundColor Green
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

Write-Host "`n  Querying existing subscription role assignments for assignee..." -ForegroundColor Yellow
$assignedRolesJson = az role assignment list --assignee $AppRegistrationId --scope $Scope -o json 2>$null
$assignedRoles = if ($assignedRolesJson) { $assignedRolesJson | ConvertFrom-Json } else { @() }
$assignedRoleNames = @($assignedRoles | ForEach-Object { $_.roleDefinitionName })

if ($DryRun) {
    Write-Host "`n  --- [DryRun] Existing Azure RBAC Roles on Subscription Scope ($($assignedRoles.Count) found) ---" -ForegroundColor Cyan
    if ($assignedRoles.Count -gt 0) {
        foreach ($r in $assignedRoles) {
            Write-Host "    • $($r.roleDefinitionName) (Assigned: $($r.createdOn))" -ForegroundColor Gray
        }
    } else {
        Write-Host "    (No roles currently assigned on this scope)" -ForegroundColor Gray
    }
    Write-Host ""
}

foreach ($role in $roles) {
    if ($assignedRoleNames -contains $role) {
        if ($DryRun) {
            Write-Host "  [DryRun] ✅ Role '$role' ALREADY ASSIGNED on scope $Scope" -ForegroundColor Green
        } else {
            Write-Host "  ℹ️ Role '$role' already assigned." -ForegroundColor Yellow
        }
    } else {
        if ($DryRun) {
            Write-Host "  [DryRun] ⚡ Role '$role' NOT FOUND on scope $Scope (would assign)" -ForegroundColor Magenta
        } else {
            Write-Host "  Assigning role '$role'..." -ForegroundColor Yellow
            az role assignment create --assignee $AppRegistrationId --role $role --scope $Scope 2>$null
            Write-Host "  ✅ Role '$role' confirmed." -ForegroundColor Green
        }
    }
}

# ------------------------------------------------------------------------------
# 4. GitHub Environment Variables & Secrets Configuration
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Configuring GitHub Environment Variables & Secrets..." -ForegroundColor Cyan

# 4a. Query & Verify GitHub Environment Variables
Write-Host "  Querying existing GitHub environment variables for '$EnvironmentName'..." -ForegroundColor Yellow
$existingVarsJson = gh variable list --repo $GitHubRepo --env $EnvironmentName --json name,value 2>$null
$existingVars = if ($existingVarsJson) { $existingVarsJson | ConvertFrom-Json } else { @() }

if ($DryRun) {
    Write-Host "`n  --- [DryRun] Existing GitHub Variables for Environment '$EnvironmentName' ($($existingVars.Count) found) ---" -ForegroundColor Cyan
    if ($existingVars.Count -gt 0) {
        foreach ($v in $existingVars) {
            Write-Host "    • $($v.name) = $($v.value)" -ForegroundColor Gray
        }
    } else {
        Write-Host "    (No variables currently configured in '$EnvironmentName')" -ForegroundColor Gray
    }
    Write-Host ""
}

$envVars = @{
    "AZURE_CLIENT_ID"       = $AppRegistrationId
    "AZURE_SUBSCRIPTION_ID" = $SubscriptionId
    "AZURE_TENANT_ID"       = $TenantId
}

foreach ($kv in $envVars.GetEnumerator()) {
    $matchVar = $existingVars | Where-Object { $_.name -eq $kv.Key }
    if ($matchVar) {
        if ($matchVar.value -eq $kv.Value) {
            if ($DryRun) {
                Write-Host "  [DryRun] ✅ Variable '$($kv.Key)' ALREADY CONFIGURED with matching value" -ForegroundColor Green
            } else {
                Write-Host "  ℹ️ Variable '$($kv.Key)' already set." -ForegroundColor Yellow
            }
        } else {
            if ($DryRun) {
                Write-Host "  [DryRun] ⚠️ Variable '$($kv.Key)' exists but value differs (Current: '$($matchVar.value)', Target: '$($kv.Value)') (would update)" -ForegroundColor Magenta
            } else {
                gh variable set $kv.Key --repo $GitHubRepo --env $EnvironmentName --body $kv.Value
                Write-Host "  ✅ GitHub Variable '$($kv.Key)' updated." -ForegroundColor Green
            }
        }
    } else {
        if ($DryRun) {
            Write-Host "  [DryRun] ⚡ Variable '$($kv.Key)' NOT FOUND (would create: '$($kv.Value)')" -ForegroundColor Magenta
        } else {
            gh variable set $kv.Key --repo $GitHubRepo --env $EnvironmentName --body $kv.Value
            Write-Host "  ✅ GitHub Variable '$($kv.Key)' set." -ForegroundColor Green
        }
    }
}

# Purge any legacy plaintext variables if present from previous configurations (ADR-077 compliance)
if ($DryRun) {
    $legacyVars = $existingVars | Where-Object { $_.name -in @("SUPER_ADMIN_EMAIL", "REQUIRE_MOBILE_VERIFICATION") }
    if ($legacyVars) {
        foreach ($lv in $legacyVars) {
            Write-Host "  [DryRun] ⚠️ Legacy plaintext variable '$($lv.name)' detected (would purge per ADR-077)" -ForegroundColor Magenta
        }
    }
} else {
    gh variable delete SUPER_ADMIN_EMAIL --repo $GitHubRepo --env $EnvironmentName 2>$null
    gh variable delete REQUIRE_MOBILE_VERIFICATION --repo $GitHubRepo --env $EnvironmentName 2>$null
}

# 4b. Query & Verify GitHub Environment Secrets
Write-Host "`n  Querying existing GitHub environment secrets for '$EnvironmentName'..." -ForegroundColor Yellow
$existingSecretsJson = gh secret list --repo $GitHubRepo --env $EnvironmentName --json name,updatedAt 2>$null
$existingSecrets = if ($existingSecretsJson) { $existingSecretsJson | ConvertFrom-Json } else { @() }
$existingSecretNames = @($existingSecrets | ForEach-Object { $_.name })

if ($DryRun) {
    Write-Host "`n  --- [DryRun] Existing GitHub Secrets for Environment '$EnvironmentName' ($($existingSecrets.Count) found) ---" -ForegroundColor Cyan
    if ($existingSecrets.Count -gt 0) {
        foreach ($s in $existingSecrets) {
            Write-Host "    • $($s.name) (Last updated: $($s.updatedAt))" -ForegroundColor Gray
        }
    } else {
        Write-Host "    (No secrets currently configured in '$EnvironmentName')" -ForegroundColor Gray
    }
    Write-Host ""
}

# Helper function to check and set secret
function Set-SecretItem {
    param (
        [string]$SecretName,
        [string]$SecretValue,
        [string]$DisplayName
    )

    $alreadyExists = $existingSecretNames -contains $SecretName

    if ($alreadyExists) {
        $secretMeta = $existingSecrets | Where-Object { $_.name -eq $SecretName }
        if ($DryRun) {
            Write-Host "  [DryRun] ℹ️ Secret '$SecretName' ALREADY EXISTS (Updated: $($secretMeta.updatedAt)) - would refresh/overwrite" -ForegroundColor Yellow
        } else {
            gh secret set $SecretName --repo $GitHubRepo --env $EnvironmentName --body $SecretValue
            Write-Host "  ✅ $DisplayName refreshed in environment '$EnvironmentName'." -ForegroundColor Green
        }
    } else {
        if ($DryRun) {
            Write-Host "  [DryRun] ⚡ Secret '$SecretName' NOT FOUND (would create in environment '$EnvironmentName')" -ForegroundColor Magenta
        } else {
            gh secret set $SecretName --repo $GitHubRepo --env $EnvironmentName --body $SecretValue
            Write-Host "  ✅ $DisplayName stored in environment '$EnvironmentName'." -ForegroundColor Green
        }
    }
}

# 4c. Cryptographic JWT Signing Key
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$SecureJwtKey = [Convert]::ToBase64String($bytes)
Set-SecretItem -SecretName "JWT_KEY" -SecretValue $SecureJwtKey -DisplayName "Cryptographic JWT_KEY (256-bit HMAC)"

# 4d. Administrative Identity & Verification (Strictly Secret-Governed per ADR-077)
if (-not [string]::IsNullOrWhiteSpace($SuperAdminEmail)) {
    Set-SecretItem -SecretName "SUPER_ADMIN_EMAIL" -SecretValue $SuperAdminEmail -DisplayName "SUPER_ADMIN_EMAIL encrypted secret"
}

Set-SecretItem -SecretName "REQUIRE_MOBILE_VERIFICATION" -SecretValue $RequireMobileVerification -DisplayName "REQUIRE_MOBILE_VERIFICATION encrypted secret"

# 4e. Gemini API Key
if ([string]::IsNullOrWhiteSpace($GeminiApiKey)) {
    if ($existingSecretNames -contains "GEMINI_API_KEY") {
        if ($DryRun) {
            $geminiMeta = $existingSecrets | Where-Object { $_.name -eq "GEMINI_API_KEY" }
            Write-Host "  [DryRun] ✅ Secret 'GEMINI_API_KEY' ALREADY EXISTS (Updated: $($geminiMeta.updatedAt))" -ForegroundColor Green
        } else {
            Write-Host "  ℹ️ GEMINI_API_KEY already exists in GitHub environment '$EnvironmentName'." -ForegroundColor Green
        }
    } else {
        Write-Host "  ℹ️ Note: GEMINI_API_KEY was not passed via parameter and does not exist in '$EnvironmentName'." -ForegroundColor Yellow
        Write-Host "     To set it manually, execute:" -ForegroundColor Yellow
        Write-Host "     gh secret set GEMINI_API_KEY --repo $GitHubRepo --env $EnvironmentName --body '<your-key>'" -ForegroundColor Cyan
    }
} else {
    Set-SecretItem -SecretName "GEMINI_API_KEY" -SecretValue $GeminiApiKey -DisplayName "GEMINI_API_KEY secret"
}

# 4f. Azure OpenAI API Key (Optional)
if (-not [string]::IsNullOrWhiteSpace($AzureOpenAiApiKey)) {
    Set-SecretItem -SecretName "AZURE_OPENAI_API_KEY" -SecretValue $AzureOpenAiApiKey -DisplayName "AZURE_OPENAI_API_KEY secret"
}

# ------------------------------------------------------------------------------
# 5. Summary & Next Steps
# ------------------------------------------------------------------------------
Write-Host "`n[5/5] Verification Summary" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "🎉 PRE-DEPLOYMENT PREREQUISITES ARE FULLY CONFIGURED!" -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "You are now ready to trigger the two-stage deployment pipelines in GitHub Actions:" -ForegroundColor Yellow
Write-Host @"
# Stage 1: Provision Infrastructure Foundation (VNet, ACA Environment, SMB Storage, Key Vault, ACR)
gh workflow run 'azure-infra-deploy.yml' `
  --repo $GitHubRepo `
  --ref $GitBranch `
  -f resourceGroupName=rg-dietdost-$EnvironmentName `
  -f location=centralindia `
  -f environment=$EnvironmentName `
  -f fileShareQuotaGb=1

# Stage 2: Build Container, Run Tests & Deploy Application Workload (ACA App Revision)
gh workflow run 'azure-app-deploy.yml' `
  --repo $GitHubRepo `
  --ref $GitBranch `
  -f resourceGroupName=rg-dietdost-$EnvironmentName `
  -f location=centralindia `
  -f provisionInfra=false `
  -f allowDemoUsers=true `
  -f enableCustomDomain=false `
  -f environment=$EnvironmentName
"@ -ForegroundColor White
Write-Host "==============================================================================" -ForegroundColor Green

# ==============================================================================
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
# ==============================================================================
# Diet-Dost Cloud Developer & Admin Access Provisioning (PowerShell)
# Target: Azure Key Vault (Data Plane) + Azure Storage Files SMB Share
# Environment: dev (rg-dietdost-dev)
# ==============================================================================

[CmdletBinding()]
param(
    [string]$ResourceGroupName = "rg-dietdost-dev",
    [string]$EnvironmentName = "dev",
    [string]$UserEmail = "nikunj_banker@hotmail.com",
    [string]$ClientIp = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "        DIET-DOST DEVELOPER & ADMIN CLOUD ACCESS PROVISIONING                 " -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "Resource Group : $ResourceGroupName" -ForegroundColor Yellow
Write-Host "Environment    : $EnvironmentName" -ForegroundColor Yellow
Write-Host "Target User    : $UserEmail" -ForegroundColor Yellow
Write-Host "------------------------------------------------------------------------------"

# 1. Verify az CLI login
Write-Host "1. Verifying Azure CLI login..." -ForegroundColor Cyan
$currentAccountJson = (az account show -o json 2>$null)
if (-not $currentAccountJson) {
    Write-Error "Not signed into Azure CLI. Please run 'az login' before running this script."
}
$account = ($currentAccountJson | ConvertFrom-Json)
$subscriptionId = $account.id
Write-Host "   Authenticated Subscription: $($account.name) ($subscriptionId)" -ForegroundColor Green

# 2. Resolve User Object ID
Write-Host "2. Resolving Microsoft Entra ID Object ID for $UserEmail..." -ForegroundColor Cyan
$userObjectId = (az ad user show --id $UserEmail --query "id" -o tsv 2>$null)
if (-not $userObjectId) {
    $userObjectId = (az ad signed-in-user show --query "id" -o tsv 2>$null)
}
if (-not $userObjectId) {
    Write-Error "Could not resolve Entra ID Object ID for $UserEmail."
}
Write-Host "   Resolved User Object ID: $userObjectId" -ForegroundColor Green

# 3. Resolve Current Client Public IP Address
if ([string]::IsNullOrWhiteSpace($ClientIp)) {
    Write-Host "3. Detecting current public IP address via https://api.ipify.org..." -ForegroundColor Cyan
    try {
        $ClientIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10).Trim()
        Write-Host "   Detected Public IP: $ClientIp" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to auto-detect public IP. Please specify -ClientIp manually if needed."
    }
} else {
    Write-Host "3. Using specified client IP: $ClientIp" -ForegroundColor Cyan
}

# 4. Resolve Target Azure Key Vault
Write-Host "4. Resolving Azure Key Vault in $ResourceGroupName..." -ForegroundColor Cyan
$kvName = (az keyvault list --resource-group $ResourceGroupName --query "[0].name" -o tsv)
if (-not $kvName) {
    Write-Error "No Key Vault found in resource group $ResourceGroupName."
}
Write-Host "   Target Key Vault: $kvName" -ForegroundColor Green

# 5. Configure Key Vault Data Plane RBAC & Firewall
Write-Host "5. Configuring Key Vault RBAC ('Key Vault Administrator') and Firewall..." -ForegroundColor Cyan
$kvScope = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.KeyVault/vaults/$kvName"

Write-Host "   Assigning 'Key Vault Administrator' to user..."
az role assignment create `
    --role "Key Vault Administrator" `
    --assignee-object-id $userObjectId `
    --assignee-principal-type "User" `
    --scope $kvScope `
    -o none 2>$null || Write-Host "   (Role assignment already present or updated)" -ForegroundColor DarkGray

if (-not [string]::IsNullOrWhiteSpace($ClientIp)) {
    Write-Host "   Whitelisting client IP $ClientIp in Key Vault firewall..."
    az keyvault network-rule add --name $kvName --ip-address $ClientIp -o none
    Write-Host "   Key Vault firewall rule updated successfully." -ForegroundColor Green
}

# 6. Resolve Target Azure Storage Account
Write-Host "6. Resolving Azure Storage Account in $ResourceGroupName..." -ForegroundColor Cyan
$storageAccountName = (az storage account list --resource-group $ResourceGroupName --query "[0].name" -o tsv)
if (-not $storageAccountName) {
    Write-Error "No Storage Account found in resource group $ResourceGroupName."
}
Write-Host "   Target Storage Account: $storageAccountName" -ForegroundColor Green

# 7. Configure Storage Account Data Plane RBAC & Firewall
Write-Host "7. Configuring Storage Account RBAC ('Storage File Data Privileged Contributor') and Firewall..." -ForegroundColor Cyan
$storageScope = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName"

Write-Host "   Assigning 'Storage File Data Privileged Contributor' to user..."
az role assignment create `
    --role "Storage File Data Privileged Contributor" `
    --assignee-object-id $userObjectId `
    --assignee-principal-type "User" `
    --scope $storageScope `
    -o none 2>$null || Write-Host "   (Role assignment already present or updated)" -ForegroundColor DarkGray

if (-not [string]::IsNullOrWhiteSpace($ClientIp)) {
    Write-Host "   Whitelisting client IP $ClientIp in Storage Account firewall..."
    az storage account network-rule add --resource-group $ResourceGroupName --account-name $storageAccountName --ip-address $ClientIp -o none
    Write-Host "   Storage Account firewall rule updated successfully." -ForegroundColor Green
}

Write-Host "------------------------------------------------------------------------------"
Write-Host "✅ Azure Developer & Admin Access Provisioning Complete!" -ForegroundColor Green
Write-Host "   - Azure Key Vault '$kvName': Secrets viewing unlocked in Azure Portal." -ForegroundColor Green
Write-Host "   - Azure Storage '$storageAccountName': File share 'dietdost-data-$EnvironmentName' unlocked in Azure Portal." -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Cyan

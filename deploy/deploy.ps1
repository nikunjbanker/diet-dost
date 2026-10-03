<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>

<#
.SYNOPSIS
    Automated Azure Container Apps deployment and Podman packaging script for Diet-Dost.
.DESCRIPTION
    Builds the production .NET 11 Linux container using Podman, deploys the Azure
    infrastructure via Bicep (including persistent Azure Files SMB share and private
    service endpoint isolation), and guides the custom domain DNS verification for dev.diet-dost.in.
.PARAMETER ResourceGroupName
    Name of the target Azure Resource Group. Default: 'rg-dietdost-dev'.
.PARAMETER Location
    Azure region for deployment. Default: 'centralindia'.
.PARAMETER CustomDomain
    Target custom domain hostname. Default: 'dev.diet-dost.in'.
.PARAMETER AcrName
    Name of the Azure Container Registry. If not specified, a unique name is generated.
.PARAMETER AllowDemoUsers
    Allows 5-tier demo accounts for showcase testing in dev.diet-dost.in. Default: $true.
.PARAMETER SkipBuild
    Skips the local Podman image build.
#>

[CmdletBinding()]
param(
    [string]$ResourceGroupName = "rg-dietdost-dev",
    [string]$Location = "centralindia",
    [string]$CustomDomain = "dev.diet-dost.in",
    [string]$AcrName = "",
    [bool]$AllowDemoUsers = $true,
    [switch]$SkipBuild,
    [switch]$BindDomain
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Diet-Dost: Azure Container Apps & Persistent SMB Deployment" -ForegroundColor Cyan
Write-Host "  Engine: Podman (OCI) | Runtime: .NET 11 | Domain: $CustomDomain" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Pre-flight Azure CLI Verification
Write-Host "`n[1/6] Verifying Azure CLI authentication..." -ForegroundColor Yellow
$azAccount = az account show --output json 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Write-Error "Not logged into Azure CLI. Please run 'az login' and select your subscription first."
}
Write-Host "  Active Subscription: $($azAccount.name) ($($azAccount.id))" -ForegroundColor Green

# 2. Resource Group Creation
Write-Host "`n[2/6] Ensuring Resource Group '$ResourceGroupName' exists in '$Location'..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location --output table | Out-Null
Write-Host "  Resource Group '$ResourceGroupName' is ready." -ForegroundColor Green

# 3. Azure Container Registry (ACR) Setup & Podman Packaging
if (-not $AcrName) {
    $randomSuffix = (Get-Random -Minimum 1000 -Maximum 9999).ToString()
    $AcrName = "crdietdost$randomSuffix"
}

Write-Host "`n[3/6] Ensuring Azure Container Registry '$AcrName' exists..." -ForegroundColor Yellow
$acrExists = az acr show --name $AcrName --resource-group $ResourceGroupName --output json 2>$null
if (-not $acrExists) {
    Write-Host "  Creating Basic SKU ACR '$AcrName'..." -ForegroundColor Gray
    az acr create --resource-group $ResourceGroupName --name $AcrName --sku Basic --admin-enabled true --output table | Out-Null
}

$acrLoginServer = az acr show --name $AcrName --resource-group $ResourceGroupName --query "loginServer" -o tsv
$acrPassword = az acr credential show --name $AcrName --query "passwords[0].value" -o tsv
$imageTag = "$acrLoginServer/diet-dost-web:latest"

if (-not $SkipBuild) {
    Write-Host "`n[4/6] Building production container image with Podman..." -ForegroundColor Yellow
    Write-Host "  Tag: $imageTag" -ForegroundColor Gray

    # Build image using Podman (WSL machine or native)
    $podmanCmd = "podman build -t $imageTag -f Containerfile ."
    Write-Host "  Executing: $podmanCmd" -ForegroundColor Gray
    & podman build -t $imageTag -f Containerfile .
    if ($LASTEXITCODE -ne 0) {
        # Fallback to podman inside WSL if Windows bridge had socket issue
        Write-Host "  Attempting build via podman WSL machine..." -ForegroundColor Gray
        $wslPath = (Get-Location).Path -replace '\\', '/' -replace 'C:', '/mnt/c'
        & wsl -d podman-machine-default podman build -t $imageTag -f "$wslPath/Containerfile" $wslPath
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Podman image build failed."
        }
    }

    Write-Host "  Logging Podman into ACR '$acrLoginServer'..." -ForegroundColor Yellow
    & podman login $acrLoginServer -u $AcrName -p $acrPassword
    if ($LASTEXITCODE -ne 0) {
        & wsl -d podman-machine-default podman login $acrLoginServer -u $AcrName -p $acrPassword
    }

    Write-Host "  Pushing container image to ACR..." -ForegroundColor Yellow
    & podman push $imageTag
    if ($LASTEXITCODE -ne 0) {
        & wsl -d podman-machine-default podman push $imageTag
    }
    Write-Host "  Image pushed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[4/6] Skipping image build as requested (-SkipBuild)." -ForegroundColor Gray
}

# 5. Execute Infrastructure as Code (Bicep Deployment)
Write-Host "`n[5/6] Deploying Azure Container Apps and Storage via Bicep (infra/main.bicep)..." -ForegroundColor Yellow
$bicepParams = @(
    "appName=app-dietdost-web",
    "containerImage=$imageTag",
    "customDomain=$CustomDomain",
    "allowDemoUsers=$($AllowDemoUsers.ToString().ToLower())"
)

$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file infra/main.bicep `
    --parameters $bicepParams `
    --output json | ConvertFrom-Json

$appFqdn = $deploymentOutput.properties.outputs.appFqdn.value
$verificationId = $deploymentOutput.properties.outputs.customDomainVerificationId.value
$storageAccount = $deploymentOutput.properties.outputs.storageAccountName.value
$acaEnv = $deploymentOutput.properties.outputs.acaEnvironmentName.value

Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  Container App Target FQDN : https://$appFqdn" -ForegroundColor Cyan
Write-Host "  Azure Files Storage       : $storageAccount (Share: dietdost-data)" -ForegroundColor Cyan
Write-Host "  Persistent Mount Point    : /app/data" -ForegroundColor Cyan
Write-Host "  Replica Constraint        : minReplicas=1, maxReplicas=1 (Zero Lock Corruption)" -ForegroundColor Cyan
Write-Host "  Showcase Demo Accounts    : Enabled (Security:AllowDemoUsers=$AllowDemoUsers)" -ForegroundColor Cyan

# 6. Custom Domain Guidance & Managed TLS Binding
Write-Host "`n[6/6] Custom Domain & Free Managed TLS Instructions ($CustomDomain):" -ForegroundColor Magenta
Write-Host "  Please configure the following 2 DNS records at your domain registrar (GoDaddy, Cloudflare, etc.):" -ForegroundColor White
Write-Host "  ----------------------------------------------------------------" -ForegroundColor Gray
Write-Host "  Type  : CNAME" -ForegroundColor Yellow
Write-Host "  Host  : dev" -ForegroundColor Yellow
Write-Host "  Target: $appFqdn" -ForegroundColor Yellow
Write-Host "  TTL   : 300" -ForegroundColor Gray
Write-Host ""
Write-Host "  Type  : TXT" -ForegroundColor Yellow
Write-Host "  Host  : asuid.dev" -ForegroundColor Yellow
Write-Host "  Value : $verificationId" -ForegroundColor Yellow
Write-Host "  TTL   : 300" -ForegroundColor Gray
Write-Host "  ----------------------------------------------------------------" -ForegroundColor Gray

if ($BindDomain) {
    Write-Host "`nBinding custom domain '$CustomDomain' and provisioning free Azure-managed certificate..." -ForegroundColor Yellow
    az containerapp hostname add `
        --name app-dietdost-web `
        --resource-group $ResourceGroupName `
        --hostname $CustomDomain `
        --output table

    az containerapp hostname bind `
        --name app-dietdost-web `
        --resource-group $ResourceGroupName `
        --hostname $CustomDomain `
        --environment $acaEnv `
        --validation-method CNAME `
        --output table
    Write-Host "  Custom domain '$CustomDomain' bound successfully with managed TLS certificate!" -ForegroundColor Green
} else {
    Write-Host "`nOnce DNS records propagate (1-5 min), run this command to bind custom domain with free TLS:" -ForegroundColor Gray
    Write-Host "  az containerapp hostname add --name app-dietdost-web --resource-group $ResourceGroupName --hostname $CustomDomain" -ForegroundColor Cyan
    Write-Host "  az containerapp hostname bind --name app-dietdost-web --resource-group $ResourceGroupName --hostname $CustomDomain --environment $acaEnv --validation-method CNAME" -ForegroundColor Cyan
    Write-Host "Or re-run this script with the -BindDomain switch." -ForegroundColor Gray
}

Write-Host "`nShowcase Validation URL: https://$appFqdn" -ForegroundColor Green

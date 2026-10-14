// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost End-to-End Composite Infrastructure as Code (Bicep)
// Composes: infra/infra.bicep (Foundation) + infra/app.bicep (Workload)
// ==============================================================================

@description('Environment prefix for deployed resources (e.g. dev, prod).')
param environment string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Name of the Container App.')
param appName string = 'app-dietdost-web'

@description('Container image reference (e.g. <registry>.azurecr.io/diet-dost-web:latest).')
param containerImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Custom domain name to bind (e.g. dev.diet-dost.in).')
param customDomain string = 'dev.diet-dost.in'

@description('Flag indicating whether the custom domain binding should be applied directly in Bicep.')
param enableCustomDomain bool = false

@description('Flag indicating whether 5-tier demo user accounts are enabled for showcase testing.')
param allowDemoUsers bool = true

@description('Storage quota for Azure Files SMB share in GB (defaults to 1 GB for dev environment).')
param fileShareQuotaGb int = 1

@description('Optional explicit name for Azure Container Registry (ACR). If omitted, an auto-generated unique name is used.')
param acrName string = ''

@description('Optional Gemini API Key for AI nutrition feedback.')
@secure()
param geminiApiKey string = ''

// 1. Foundation Infrastructure (VNet, Storage, SMB File Share, Log Analytics, ACA Environment, ACR)
module foundation 'infra.bicep' = {
  name: 'foundation-deployment'
  params: {
    environment: environment
    location: location
    fileShareQuotaGb: fileShareQuotaGb
    acrName: acrName
  }
}

// 2. Application Workload (Container App + SMB Volume Mount + Single Replica + Ingress)
module workload 'app.bicep' = {
  name: 'workload-deployment'
  params: {
    location: location
    appName: appName
    acaEnvironmentId: foundation.outputs.acaEnvironmentId
    containerImage: containerImage
    customDomain: customDomain
    enableCustomDomain: enableCustomDomain
    allowDemoUsers: allowDemoUsers
    storageMountName: foundation.outputs.storageMountName
    geminiApiKey: geminiApiKey
    keyVaultUri: foundation.outputs.keyVaultUri
    managedIdentityId: foundation.outputs.managedIdentityId
    managedIdentityClientId: foundation.outputs.managedIdentityClientId
  }
}

// Outputs
output appFqdn string = workload.outputs.appFqdn
output customDomainVerificationId string = foundation.outputs.customDomainVerificationId
output staticIp string = foundation.outputs.staticIp
output storageAccountName string = foundation.outputs.storageAccountName
output fileShareName string = foundation.outputs.fileShareName
output acaEnvironmentName string = foundation.outputs.acaEnvironmentName
output containerAppName string = workload.outputs.containerAppName
output acrName string = foundation.outputs.acrName
output acrLoginServer string = foundation.outputs.acrLoginServer
output keyVaultName string = foundation.outputs.keyVaultName
output keyVaultUri string = foundation.outputs.keyVaultUri
output managedIdentityId string = foundation.outputs.managedIdentityId

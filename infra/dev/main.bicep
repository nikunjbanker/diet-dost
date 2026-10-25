// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost End-to-End Composite Infrastructure as Code (Bicep)
// Composes: infra/dev/infra.bicep (Foundation) + infra/dev/app.bicep (Workload)
// ==============================================================================

@description('Environment prefix for deployed resources (e.g. dev, prod).')
param environment string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Name of the Container App.')
param appName string = 'app-dietdost-web-${environment}'

@description('Container image reference (e.g. <registry>.azurecr.io/diet-dost-web:latest).')
param containerImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Custom domain name to bind (e.g. dev.diet-dost.in).')
param customDomain string = '${environment}.diet-dost.in'

@description('Flag indicating whether the custom domain binding should be applied directly in Bicep.')
param enableCustomDomain bool = false

@description('Flag indicating whether 5-tier demo user accounts are enabled for showcase testing.')
param allowDemoUsers bool = true

@description('Storage quota for Azure Files SMB share in GB (defaults to 1 GB for dev environment).')
param fileShareQuotaGb int = 1

@description('Optional explicit name for Azure Container Registry (ACR). If omitted, an auto-generated unique name is used.')
param acrName string = ''

@description('Optional explicit name for Azure Key Vault. If omitted, an auto-generated unique name is used.')
param keyVaultName string = ''

@description('Name of the delegated subnet for Azure Container Apps infrastructure.')
param acaSubnetName string = 'snet-aca-infra'

@description('Optional Gemini API Key for AI nutrition feedback.')
@secure()
param geminiApiKey string = ''

@description('Database persistence provider (Sqlite, AzureSql, SqlServer).')
param databaseProvider string = 'AzureSql'

@description('Optional Azure SQL Connection String (for Managed Identity or SQL auth).')
@secure()
param azureSqlConnectionString string = ''

@description('Flag indicating whether Azure SQL Database Serverless Free Tier should be provisioned (default true for AzureSql database provider).')
param deployAzureSql bool = true

@description('Optional explicit name for Azure SQL Server. If omitted, a unique name is derived.')
param sqlServerName string = ''

@description('SQL Server Administrator login username (SQL Authentication).')
param sqlAdminLogin string = 'sqladmin'

@description('SQL Server Administrator login password (SQL Authentication).')
@secure()
param sqlAdminPassword string = ''

@description('Optional client IP address of local developer workstation to permit through SQL Server firewall.')
param allowedClientIp string = ''

// 1. Foundation Infrastructure (VNet, Storage, SMB File Share, Log Analytics, ACA Environment, ACR)
module foundation 'infra.bicep' = {
  name: 'foundation-deployment'
  params: {
    environment: environment
    location: location
    fileShareQuotaGb: fileShareQuotaGb
    acrName: acrName
    keyVaultName: keyVaultName
    acaSubnetName: acaSubnetName
    deployAzureSql: deployAzureSql
    sqlServerName: sqlServerName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    allowedClientIp: allowedClientIp
  }
}

// 2. Application Workload (Container App + SMB Volume Mount + Single Replica + Ingress)
module workload 'app.bicep' = {
  name: 'workload-deployment'
  params: {
    environment: environment
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
    appInsightsConnectionString: foundation.outputs.appInsightsConnectionString
    databaseProvider: databaseProvider
    azureSqlConnectionString: azureSqlConnectionString
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
output acaSubnetId string = foundation.outputs.acaSubnetId
output acaSubnetName string = foundation.outputs.acaSubnetName
output appInsightsName string = foundation.outputs.appInsightsName
output appInsightsId string = foundation.outputs.appInsightsId
output appInsightsConnectionString string = foundation.outputs.appInsightsConnectionString
output sqlServerFqdn string = foundation.outputs.sqlServerFqdn
output sqlServerName string = foundation.outputs.sqlServerName
output sqlDatabaseName string = foundation.outputs.sqlDatabaseName

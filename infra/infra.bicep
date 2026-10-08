// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost Cloud Infrastructure as Code (Bicep) - Foundation Layer
// Target: ACA Managed Environment + Persistent Azure Files SMB Share + VNet Perimeter
// Pipeline: .github/workflows/azure-infra-deploy.yml (Aspire Infrastructure Pipeline)
// ==============================================================================

@description('Environment prefix for deployed resources (e.g. dev, prod).')
param environment string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Storage quota for Azure Files SMB share in GB.')
param fileShareQuotaGb int = 10

@description('Optional explicit name for Azure Container Registry (ACR). If omitted, an auto-generated unique name is used.')
param acrName string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = take('stgdietdost${uniqueSuffix}', 24)
var actualAcrName = !empty(acrName) ? acrName : take('crdietdost${uniqueSuffix}', 50)
var fileShareName = 'dietdost-data'
var logAnalyticsName = 'log-dietdost-${environment}'
var acaEnvName = 'cae-dietdost-${environment}'
var vnetName = 'vnet-dietdost-${environment}'

// ------------------------------------------------------------------------------
// 1. Virtual Network with Delegated Subnet for ACA and Storage Service Endpoint
// ------------------------------------------------------------------------------
resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'snet-aca-infra'
        properties: {
          addressPrefix: '10.0.0.0/23'
          delegations: [
            {
              name: 'aca-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
            }
          ]
        }
      }
    ]
  }
}

// ------------------------------------------------------------------------------
// 2. Azure Storage Account with Azure Files SMB Share (Private VNet Perimeter)
// ------------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: [
        {
          id: vnet.properties.subnets[0].id
          action: 'Allow'
        }
      ]
    }
  }
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileServices
  name: fileShareName
  properties: {
    shareQuota: fileShareQuotaGb
  }
}

// ------------------------------------------------------------------------------
// 3. Log Analytics Workspace
// ------------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ------------------------------------------------------------------------------
// 4. Azure Container Apps Managed Environment
// ------------------------------------------------------------------------------
resource acaEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: acaEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      internal: false
      infrastructureSubnetId: vnet.properties.subnets[0].id
    }
  }
}

// ------------------------------------------------------------------------------
// 5. Durable Storage Mount Link (Azure Files SMB -> ACA Environment)
// ------------------------------------------------------------------------------
resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: acaEnvironment
  name: 'dietdoststorage'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// ------------------------------------------------------------------------------
// 6. Azure Container Registry (ACR) for Container Images
// ------------------------------------------------------------------------------
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: actualAcrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
  tags: {
    'aspire-resource-name': 'cae-dietdost-acr'
    environment: environment
  }
}

// ------------------------------------------------------------------------------
// Outputs for Application Deployment Pipeline
// ------------------------------------------------------------------------------
output acaEnvironmentId string = acaEnvironment.id
output acaEnvironmentName string = acaEnvironment.name
output customDomainVerificationId string = acaEnvironment.properties.customDomainConfiguration.customDomainVerificationId
output staticIp string = acaEnvironment.properties.staticIp
output storageAccountName string = storageAccount.name
output fileShareName string = fileShare.name
output storageMountName string = envStorage.name
output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer

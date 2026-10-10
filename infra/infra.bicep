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

@description('Storage quota for Azure Files SMB share in GB (defaults to 1 GB for dev environment).')
param fileShareQuotaGb int = 1

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
// checkov:skip=CKV_AZURE_43: Storage account uses uniqueString prefix compliant with Azure 3-24 char naming rules.
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
// checkov:skip=CKV_AZURE_139: ACR Basic SKU used for cost optimization in development; Premium SKU required for private networking.
// checkov:skip=CKV_AZURE_163: Vulnerability scanning requires Microsoft Defender for Containers on ACR Premium tier.
// checkov:skip=CKV_AZURE_166: Quarantine and content trust require ACR Premium SKU.
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: actualAcrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
  tags: {
    'aspire-resource-name': 'cae-dietdost-acr'
    environment: environment
  }
}

// ------------------------------------------------------------------------------
// 7. Azure Key Vault & User-Assigned Managed Identity for Secure Cloud Secrets
// ------------------------------------------------------------------------------
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-dietdost-${environment}'
  location: location
  tags: {
    'aspire-resource-name': 'dietdost-identity'
    environment: environment
  }
}

// Built-in Role Definition: AcrPull (7f951dda-4ed3-4680-a7ca-43fe172d538d)
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, managedIdentity.id, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

var keyVaultName = take('kvdietdost${uniqueSuffix}', 24)

// checkov:skip=CKV_AZURE_109: Key Vault firewall defaultAction Allow is required for GitHub Actions runners to seed secrets in dev tier.
// checkov:skip=CKV_AZURE_189: Key Vault public network access is required for CI/CD automation without private self-hosted runners in dev tier.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
  tags: {
    'aspire-resource-name': 'dietdost-kv'
    environment: environment
  }
}

// Built-in Role Definition: Key Vault Secrets User (4633458b-17de-408a-b874-0445c86b69e6)
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
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
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId

// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost Production Infrastructure as Code (Bicep)
// Target: Azure Container Apps (ACA) + Azure Files SMB Persistent Volume (/app/data)
// Security: Private VNet Service Endpoint isolation for Azure Files SMB
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

@description('Storage quota for Azure Files SMB share in GB.')
param fileShareQuotaGb int = 10

@description('Optional Gemini API Key for AI nutrition feedback.')
@secure()
param geminiApiKey string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = take('stgdietdost${uniqueSuffix}', 24)
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
// 2. Azure Storage Account with Azure Files SMB Share (Private Access Only)
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
// 6. Container App (Web Gateway & Mobile BFF)
// ------------------------------------------------------------------------------
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  dependsOn: [
    envStorage
  ]
  properties: {
    managedEnvironmentId: acaEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        customDomains: enableCustomDomain ? [
          {
            name: customDomain
            bindingType: 'SniEnabled'
          }
        ] : []
      }
      secrets: !empty(geminiApiKey) ? [
        {
          name: 'gemini-api-key'
          value: geminiApiKey
        }
      ] : []
    }
    template: {
      containers: [
        {
          name: 'diet-dost-web'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Database__Provider'
              value: 'Sqlite'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              value: 'Data Source=/app/data/diet_dost.db;Cache=Shared'
            }
            {
              name: 'Storage__WebRootPath'
              value: '/app/data/wwwroot'
            }
            {
              name: 'Security__AllowDemoUsers'
              value: string(allowDemoUsers)
            }
            {
              name: 'AI__Google__ApiKey'
              secretRef: !empty(geminiApiKey) ? 'gemini-api-key' : null
              value: empty(geminiApiKey) ? '' : null
            }
          ]
          volumeMounts: [
            {
              volumeName: 'dietdoststorage'
              mountPath: '/app/data'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1 // SQLite Zero Data Loss Golden Rule 1
      }
      volumes: [
        {
          name: 'dietdoststorage'
          storageType: 'AzureFile'
          storageName: 'dietdoststorage'
        }
      ]
    }
  }
}

// ------------------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------------------
output appFqdn string = containerApp.properties.configuration.ingress.fqdn
output customDomainVerificationId string = acaEnvironment.properties.customDomainConfiguration.customDomainVerificationId
output staticIp string = acaEnvironment.properties.staticIp
output storageAccountName string = storageAccount.name
output fileShareName string = fileShare.name
output acaEnvironmentName string = acaEnvironment.name
output containerAppName string = containerApp.name

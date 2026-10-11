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
var nsgName = 'nsg-dietdost-${environment}'

// ------------------------------------------------------------------------------
// 1. Network Security Group (NSG) with Zero-Trust Subnet Isolation
// ------------------------------------------------------------------------------
resource nsg 'Microsoft.Network/networkSecurityGroups@2023-05-01' = {
  name: nsgName
  location: location
  properties: {
    securityRules: [
      {
        name: 'Allow-HTTPS-Inbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
        }
      }
      {
        name: 'Allow-AzureLoadBalancer-Inbound'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'Allow-VNet-Internal-Inbound'
        properties: {
          priority: 120
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: 'VirtualNetwork'
          destinationPortRange: '*'
        }
      }
      {
        name: 'Deny-All-Other-Inbound'
        properties: {
          priority: 4000
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        name: 'Allow-Storage-SMB-Outbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'Storage'
          destinationPortRange: '445'
        }
      }
      {
        name: 'Allow-AzureCloud-HTTPS-Outbound'
        properties: {
          priority: 110
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'AzureCloud'
          destinationPortRange: '443'
        }
      }
      {
        name: 'Allow-DNS-Outbound'
        properties: {
          priority: 120
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '53'
        }
      }
      {
        name: 'Allow-NTP-Outbound'
        properties: {
          priority: 130
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Udp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '123'
        }
      }
      {
        name: 'Allow-Internet-HTTPS-Outbound'
        properties: {
          priority: 140
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'Internet'
          destinationPortRange: '443'
        }
      }
      {
        name: 'Allow-VNet-Internal-Outbound'
        properties: {
          priority: 150
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: 'VirtualNetwork'
          destinationPortRange: '*'
        }
      }
      {
        name: 'Deny-All-Other-Outbound'
        properties: {
          priority: 4000
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

// ------------------------------------------------------------------------------
// 2. Virtual Network with Delegated Subnet for ACA and Storage Service Endpoint
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
          networkSecurityGroup: {
            id: nsg.id
          }
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
            {
              service: 'Microsoft.KeyVault'
            }
          ]
        }
      }
    ]
  }
}

// ------------------------------------------------------------------------------
// 3. Azure Storage Account with Azure Files SMB 3.1.1 Encrypted Share
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
  properties: {
    protocolSettings: {
      smb: {
        versions: 'SMB3.1.1'
        channelEncryption: 'AES-128-GCM;AES-256-GCM'
        multichannel: {
          enabled: false
        }
      }
    }
    shareDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileServices
  name: fileShareName
  properties: {
    shareQuota: fileShareQuotaGb
  }
}

// ------------------------------------------------------------------------------
// 4. Log Analytics Workspace
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
// 5. Azure Container Apps Managed Environment
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
// 6. Durable Storage Mount Link (Azure Files SMB -> ACA Environment)
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
// 7. Azure Container Registry (ACR) for Container Images
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
// 8. Azure Key Vault & User-Assigned Managed Identity for Secure Cloud Secrets
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
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: [
        {
          id: vnet.properties.subnets[0].id
        }
      ]
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
// 9. Key Vault Diagnostic Logging (Audit Security Baseline)
// ------------------------------------------------------------------------------
resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-kv-${environment}'
  scope: keyVault
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
      {
        category: 'AzurePolicyEvaluationDetails'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ------------------------------------------------------------------------------
// 10. Automated Security Metric Alert: Key Vault Unauthorized Access
// ------------------------------------------------------------------------------
resource keyVaultUnauthorizedAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-kv-unauthorized-${environment}'
  location: 'global'
  properties: {
    description: 'Alert when Key Vault unauthorized requests (401/403) exceed security baseline threshold.'
    severity: 1
    enabled: true
    scopes: [
      keyVault.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighUnauthorizedRequests'
          metricName: 'ServiceApiResult'
          dimensions: [
            {
              name: 'StatusCodeRange'
              operator: 'Include'
              values: [
                '401'
                '403'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
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
output nsgId string = nsg.id
output nsgName string = nsg.name

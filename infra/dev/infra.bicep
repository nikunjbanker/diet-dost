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

@description('Optional explicit name for Azure Key Vault. If omitted, an auto-generated unique name is used.')
param keyVaultName string = ''

@description('Name of the delegated subnet for Azure Container Apps infrastructure.')
param acaSubnetName string = 'snet-aca-infra'

@description('Flag indicating whether Azure SQL Database Serverless Free Tier should be provisioned.')
param deployAzureSql bool = false

@description('Optional explicit name for Azure SQL Server. If omitted, a unique name is derived.')
param sqlServerName string = ''

@description('SQL Server Administrator login username (SQL Authentication).')
param sqlAdminLogin string = 'sqladmin'

@description('SQL Server Administrator login password (SQL Authentication).')
@secure()
param sqlAdminPassword string = ''

@description('Optional client IP address of local developer workstation to permit through SQL Server firewall.')
param allowedClientIp string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = take('stgdost${environment}${uniqueSuffix}', 24)
var sanitizedAcrName = replace(replace(toLower(acrName), '-', ''), '_', '')
var actualAcrName = !empty(sanitizedAcrName) ? sanitizedAcrName : take('crdost${environment}${uniqueSuffix}', 50)
var sanitizedKeyVaultName = replace(replace(toLower(keyVaultName), '-', ''), '_', '')
var actualKeyVaultName = !empty(sanitizedKeyVaultName) ? sanitizedKeyVaultName : take('kvdost${environment}${uniqueSuffix}', 24)
var fileShareName = 'dietdost-data-${environment}'
var logAnalyticsName = 'log-dietdost-${environment}'
var appInsightsName = 'appi-dietdost-${environment}'
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
        name: 'Allow-SQL-Outbound'
        properties: {
          priority: 105
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: 'Sql'
          destinationPortRanges: [
            '1433'
            '11000-11999'
          ]
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
        name: acaSubnetName
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
            {
              service: 'Microsoft.Sql'
            }
          ]
        }
      }
    ]
  }
}

resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' existing = {
  parent: vnet
  name: acaSubnetName
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
          id: acaSubnet.id
          action: 'Allow'
        }
      ]
    }
  }
  dependsOn: [
    #disable-next-line no-unnecessary-dependson
    vnet
  ]
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
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
// 4b. Azure Application Insights (Workspace-based, Entra ID Only / DisableLocalAuth)
// ------------------------------------------------------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    DisableLocalAuth: true
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    'aspire-resource-name': 'dietdost-appinsights'
    environment: environment
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
      infrastructureSubnetId: acaSubnet.id
    }
  }
  dependsOn: [
    #disable-next-line no-unnecessary-dependson
    vnet
  ]
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

// checkov:skip=CKV_AZURE_109: Key Vault firewall defaultAction Allow is required for GitHub Actions runners to seed secrets in dev tier.
// checkov:skip=CKV_AZURE_189: Key Vault public network access is required for CI/CD automation without private self-hosted runners in dev tier.
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: actualKeyVaultName
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
          id: acaSubnet.id
        }
      ]
    }
  }
  dependsOn: [
    #disable-next-line no-unnecessary-dependson
    vnet
  ]
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

// Built-in Role Definition: Monitoring Metrics Publisher (3913510d-42f4-4e42-8a64-420c390055eb)
// Enables User-Assigned Managed Identity to ingest telemetry with DisableLocalAuth: true
resource appiMonitoringPublisherRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, managedIdentity.id, '3913510d-42f4-4e42-8a64-420c390055eb')
  scope: appInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
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
          metricNamespace: 'Microsoft.KeyVault/vaults'
          metricName: 'ServiceApiResult'
          dimensions: [
            {
              name: 'StatusCode'
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
// 11. Azure SQL Serverless Free Tier Database Module
// ------------------------------------------------------------------------------
module sql 'sql.bicep' = if (deployAzureSql) {
  name: 'sql-deployment'
  params: {
    environment: environment
    location: location
    sqlServerName: sqlServerName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    acaSubnetId: acaSubnet.id
    keyVaultName: keyVault.name
    allowedClientIp: allowedClientIp
  }
  dependsOn: [
    #disable-next-line no-unnecessary-dependson
    keyVault
    #disable-next-line no-unnecessary-dependson
    vnet
  ]
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
output acaSubnetId string = acaSubnet.id
output acaSubnetName string = acaSubnetName
output appInsightsName string = appInsights.name
output appInsightsId string = appInsights.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output sqlServerFqdn string = sql.?outputs.sqlServerFqdn ?? ''
output sqlServerName string = sql.?outputs.sqlServerName ?? ''
output sqlDatabaseName string = sql.?outputs.sqlDatabaseName ?? ''
output azureSqlConnectionStringSecretName string = sql.?outputs.connectionStringSecretName ?? ''

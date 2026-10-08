// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost Application Deployment as Code (Bicep) - Workload Layer
// Target: Azure Container Apps Revision + SMB Persistent Volume Mount (/app/data)
// Pipeline: .github/workflows/azure-app-deploy.yml (Aspire Application Pipeline)
// ==============================================================================

@description('Azure region for the container app.')
param location string = resourceGroup().location

@description('Name of the Container App.')
param appName string = 'app-dietdost-web'

@description('Resource ID of the ACA Managed Environment.')
param acaEnvironmentId string

@description('Container image reference (e.g. <registry>.azurecr.io/diet-dost-web:latest).')
param containerImage string

@description('Custom domain name to bind (e.g. dev.diet-dost.in).')
param customDomain string = 'dev.diet-dost.in'

@description('Flag indicating whether custom domain binding should be applied.')
param enableCustomDomain bool = false

@description('Flag indicating whether 5-tier demo user accounts are enabled for showcase testing.')
param allowDemoUsers bool = true

@description('Name of the durable storage mount linked to the ACA environment.')
param storageMountName string = 'dietdoststorage'

@description('Optional Gemini API Key for AI nutrition feedback.')
@secure()
param geminiApiKey string = ''

@description('Azure Key Vault URI (e.g. https://<kv-name>.vault.azure.net/) for cloud secrets.')
param keyVaultUri string = ''

@description('Resource ID of User-Assigned Managed Identity for Key Vault authentication.')
param managedIdentityId string = ''

@description('Client ID of User-Assigned Managed Identity for Key Vault authentication.')
param managedIdentityClientId string = ''

// ------------------------------------------------------------------------------
// Container App (Web Gateway & Mobile BFF Workload)
// ------------------------------------------------------------------------------
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: !empty(managedIdentityId) ? {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  } : {
    type: 'None'
  }
  properties: {
    managedEnvironmentId: acaEnvironmentId
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
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
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
            {
              name: 'KeyVault__VaultUri'
              value: keyVaultUri
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentityClientId
            }
          ]
          volumeMounts: [
            {
              volumeName: storageMountName
              mountPath: '/app/data'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1 // SQLite Zero Data Loss Golden Rule 1 (Single-writer invariant)
      }
      volumes: [
        {
          name: storageMountName
          storageType: 'AzureFile'
          storageName: storageMountName
        }
      ]
    }
  }
}

// ------------------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------------------
output appFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppName string = containerApp.name

// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost Azure DocumentDB (Free Tier) Module
// Resource: Microsoft.DocumentDB/mongoClusters (Dedicated vCore Architecture)
// Features: 32 GB Dedicated Storage (Free for Life), Zero RU Limits, Zero RU Throttling,
//           SCRAM-SHA-256 Authentication, TLS 1.2+ Transit Encryption
// Reference: https://learn.microsoft.com/en-us/azure/documentdb/free-tier
// ==============================================================================

@description('Target environment name (e.g. dev, prod).')
param environment string = 'dev'

@description('Azure region for the DocumentDB cluster.')
param location string = resourceGroup().location

@description('Optional explicit name for the DocumentDB cluster. If omitted, a unique name is derived.')
param clusterName string = ''

@description('Database name inside the DocumentDB cluster.')
param databaseName string = 'dietdost'

@description('Administrator login username for the DocumentDB cluster.')
param adminLogin string = 'docdbadmin'

@description('Administrator login password for the DocumentDB cluster.')
@secure()
param adminPassword string

@description('Name of the Azure Key Vault for storing the DocumentDB connection string.')
param keyVaultName string

@description('Optional client IP address of local developer workstation to permit through DocumentDB firewall.')
param allowedClientIp string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var actualClusterName = !empty(clusterName) ? toLower(clusterName) : take('docdb-dietdost-${environment}-${uniqueSuffix}', 40)

// ------------------------------------------------------------------------------
// 1. Azure DocumentDB Cluster - Free Tier (32 GB dedicated storage, vCore architecture)
// ------------------------------------------------------------------------------
resource docDbCluster 'Microsoft.DocumentDB/mongoClusters@2024-07-01' = {
  name: actualClusterName
  location: location
  properties: {
    compute: {
      tier: 'Free' // Azure DocumentDB Lifetime Free Tier (32 GB dedicated storage)
    }
    serverVersion: '6.0'
    storage: {
      sizeGb: 32 // 32 GB Dedicated Free Storage
    }
    administrator: {
      userName: adminLogin
      password: adminPassword
    }
  }
  tags: {
    'aspire-resource-name': 'dietdost-docdb'
    environment: environment
  }
}

// ------------------------------------------------------------------------------
// 2. Firewall Rules
// ------------------------------------------------------------------------------

// Rule 1: Allow Azure Container Apps and other Azure services
resource allowAzureServices 'Microsoft.DocumentDB/mongoClusters/firewallRules@2024-07-01' = {
  parent: docDbCluster
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Rule 2: Allow Local Developer Workstation IP (if provided)
resource allowDeveloperIp 'Microsoft.DocumentDB/mongoClusters/firewallRules@2024-07-01' = if (!empty(allowedClientIp)) {
  parent: docDbCluster
  name: 'AllowDeveloperWorkstationIp'
  properties: {
    startIpAddress: allowedClientIp
    endIpAddress: allowedClientIp
  }
}

// ------------------------------------------------------------------------------
// 3. Azure Key Vault Secret Storage (Sole Authority - Zero Plaintext in Code)
// ------------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (!empty(keyVaultName)) {
  name: keyVaultName
}

var connectionString = 'mongodb+srv://${adminLogin}:${adminPassword}@${docDbCluster.name}.mongocluster.cosmos.azure.com/${databaseName}?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000'

resource secretDocDbPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: keyVault
  name: 'documentdb-admin-password'
  properties: {
    value: adminPassword
    contentType: 'text/plain'
  }
}

resource secretDocDbConn 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: keyVault
  name: 'documentdb-connection-string'
  properties: {
    value: connectionString
    contentType: 'connection-string'
  }
}

// ------------------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------------------
output clusterId string = docDbCluster.id
output clusterName string = docDbCluster.name
output clusterFqdn string = '${docDbCluster.name}.mongocluster.cosmos.azure.com'
output databaseName string = databaseName
output connectionStringSecretName string = 'documentdb-connection-string'

// ==============================================================================
// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and
// the "Server Side Public License, v 1"; you may not use this file except
// in compliance with, at your election, the "GNU Affero General Public
// License v3.0 only" or the "Server Side Public License, v 1".
// ==============================================================================
// Diet-Dost Azure SQL Database Basic DTU Module
// Provides: Logical Server + Basic DTU Database + VNet Service Endpoint Rule
//           + Developer Firewall Rule + Key Vault Secret Persistence
// ==============================================================================

@description('Target environment name (e.g. dev, prod).')
param environment string = 'dev'

@description('Azure region for the SQL Server and database.')
param location string = resourceGroup().location

@description('Optional explicit name for Azure SQL Server. If empty, a unique name is derived.')
param sqlServerName string = ''

@description('Database name.')
param databaseName string = 'dietdost'

@description('SQL Server Administrator login username (SQL Authentication).')
param sqlAdminLogin string = 'sqladmin'

@description('SQL Server Administrator login password (SQL Authentication).')
@secure()
param sqlAdminPassword string

@description('Resource ID of the delegated Azure Container Apps subnet for Service Endpoint binding.')
param acaSubnetId string

@description('Name of the Azure Key Vault for storing the SQL connection string and admin credentials.')
param keyVaultName string

@description('Optional client IP address of the local developer workstation to allow through SQL Server firewall.')
param allowedClientIp string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var actualSqlServerName = !empty(sqlServerName) ? toLower(sqlServerName) : take('sql-dietdost-${environment}-${uniqueSuffix}', 63)

// ------------------------------------------------------------------------------
// 1. Azure SQL Logical Server (SQL Authentication, TLS 1.2)
// ------------------------------------------------------------------------------
// checkov:skip=CKV_AZURE_23: Auditing is enabled via Azure Monitor diagnostics in production.
// checkov:skip=CKV_AZURE_24: Threat detection is managed via Microsoft Defender for SQL at subscription level.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: actualSqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // Enabled to support VNet Service Endpoint rules and authorized developer client IPs
  }
  tags: {
    'aspire-resource-name': 'dietdost-sql'
    environment: environment
  }
}

// ------------------------------------------------------------------------------
// 2. Azure SQL Database - Basic DTU
// Features: 5 DTUs and a 2 GB maximum database size
// ------------------------------------------------------------------------------
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB (Azure SQL Basic DTU maximum)
  }
  tags: {
    'aspire-resource-name': 'dietdost-sqldb'
    environment: environment
  }
}

// ------------------------------------------------------------------------------
// 3. Virtual Network Rule (Service Endpoint Binding to snet-aca-infra)
// Allows Azure Container Apps traffic directly over Microsoft backbone without public internet traversal
// ------------------------------------------------------------------------------
resource sqlVnetRule 'Microsoft.Sql/servers/virtualNetworkRules@2023-08-01-preview' = if (!empty(acaSubnetId)) {
  parent: sqlServer
  name: 'allow-aca-subnet'
  properties: {
    virtualNetworkSubnetId: acaSubnetId
    ignoreMissingVnetServiceEndpoint: false
  }
}

// ------------------------------------------------------------------------------
// 4. Firewall Rules
// ------------------------------------------------------------------------------

// Allow Local Developer Workstation IP (if provided)
resource allowDeveloperIp 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (!empty(allowedClientIp)) {
  parent: sqlServer
  name: 'AllowLocalDeveloperWorkstationIp'
  properties: {
    startIpAddress: allowedClientIp
    endIpAddress: allowedClientIp
  }
}

// ------------------------------------------------------------------------------
// 5. Azure Key Vault Secret Storage (Zero Plaintext Secrets in Config/Code)
// ------------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (!empty(keyVaultName)) {
  name: keyVaultName
}

var formattedConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'

resource secretSqlPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
    contentType: 'text/plain'
  }
}

resource secretSqlConn 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: keyVault
  name: 'azure-sql-connection-string'
  properties: {
    value: formattedConnectionString
    contentType: 'connection-string'
  }
}

// ------------------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------------------
output sqlServerId string = sqlServer.id
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output connectionStringSecretName string = 'azure-sql-connection-string'

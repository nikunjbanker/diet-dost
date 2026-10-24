<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-130: Azure SQL Redirect NSG Outbound Ports and Workflow Summary Synchronization

## Status
Accepted & Implemented (NSG Outbound Ports 1433 & 11000-11999 + Workflow Step Summaries + Health Diagnostics)

## Context
Following the deployment of Azure SQL Database Serverless Free Tier, two runtime operational defects were identified:
1. **Azure SQL Worker Node Connection Timeout (Error 258)**:
   When `databaseProvider: AzureSql` was selected, the Container App failed during startup with `SqlException (0x80131904) / Win32Exception (258): The connection attempt timed out` while routing to worker node `e8c1c598fee1.tr4368.indiacentral1-a.worker.database.windows.net:11001`.
   - Azure SQL uses the **Redirect connection policy** for traffic originating inside Azure: initial connection connects to port 1433 gateway, which returns redirection to backend database cluster nodes on ports **11000–11999**.
   - Subnet NSG rule `Allow-SQL-Outbound` only permitted port 1433, causing `Deny-All-Other-Outbound` to drop data-plane TCP traffic to port 11001.
2. **Missing Parameter Visibility in Workflow Summaries**:
   The workflow summary cards (`$GITHUB_STEP_SUMMARY`) in both `azure-app-deploy.yml` and `azure-infra-deploy.yml` omitted newly introduced database parameters (`databaseProvider`, `deployAzureSql`, `sqlAdminLogin`, `allowedClientIp`), leaving operators without visibility into the active database provider.
3. **Unconditional Secret Mapping in `app.bicep`**:
   `app.bicep` unconditionally fetched `azure-sql-connection-string` from Key Vault even when `databaseProvider == 'Sqlite'`, breaking deployments when Azure SQL was not provisioned.

## Decision
1. **NSG Outbound Rule Expansion (`infra/dev/infra.bicep`)**:
   - Expanded rule `Allow-SQL-Outbound` to allow `destinationPortRanges: ['1433', '11000-11999']` targeting service tag `Sql`.
   - Satisfies Microsoft's official security standard for Azure SQL Redirect connectivity policy without opening unneeded ports.
2. **Conditional Key Vault Secret Binding (`infra/dev/app.bicep`)**:
   - `azure-sql-connection-string` is only queried from Key Vault if `databaseProvider == 'AzureSql'`.
   - In container `env`, `ConnectionStrings__AzureSqlConnection` uses `secretRef` only when `databaseProvider == 'AzureSql'`; otherwise it assigns an empty string.
3. **Pipeline Summary Synchronization & ACR Default Alignment**:
   - Updated `azure-app-deploy.yml` dispatch options and Stage 3 deployment summary cards to render `Database Provider`.
   - Linked `deployAzureSql: ${{ inputs.databaseProvider == 'AzureSql' }}` in the application deployment foundation step.
   - Updated `azure-infra-deploy.yml` dispatch options and Stage 3 summary cards to render Azure SQL provisioned state, FQDN, database name, and admin login.
   - Corrected default `acrName` in `azure-infra-deploy.yml` inputs and summary fallback from `acrdietdostdev` to `crdostdevskehxuagttzos`, aligning with the deployed dev container registry.
4. **Health Diagnostics Transparency (`DatabaseHealthCheck.cs`)**:
   - Exposes active EF Core provider name (`Microsoft.EntityFrameworkCore.SqlServer` vs `Microsoft.EntityFrameworkCore.Sqlite`) in `HealthCheckResult.Healthy` data dictionary for runtime verification via `/ready`.

## Consequences
- **Positive**: Eliminates connection timeouts on Azure SQL worker nodes; provides full visibility into active database parameters across GitHub Actions workflows; supports seamless zero-error deployments under both SQLite and Azure SQL modes.
- **Negative**: Subnet NSG outbound egress now permits ports 11000–11999 specifically scoped to Azure service tag `Sql`.

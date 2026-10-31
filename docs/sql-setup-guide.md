<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Diet-Dost: Azure SQL Database Serverless (Free Tier) Setup & Operations Guide

This guide provides the authoritative architecture, Infrastructure-as-Code (Bicep) automation, security boundaries, and local developer workstation procedures for running Diet-Dost on **Azure SQL Database Serverless Free Tier**.

---

## 1. Architectural Summary & Free Tier Quotas

Microsoft Azure provides a permanent free grant for Azure SQL Database Serverless under the following parameters:
- **Compute Tier**: General Purpose Serverless Gen5 (`GP_S_Gen5_1`).
- **Free Allowance**: **100,000 vCore-seconds per month** free for the lifetime of the Azure subscription.
- **Storage Limit**: **32 GB** included at $0 cost.
- **Auto-Pause Delay**: 60 minutes of inactivity. When paused, compute charges are **$0.00**.
- **Free Limit Exhaustion Behavior**: Configured to `'AutoPause'`, which prevents any unmetered cloud billing if the 100k vCore-second threshold is reached within a billing cycle.
- **Quota Rule**: Exactly **1 free Azure SQL database** is permitted per Azure subscription.

---

## 2. Security & Zero-Trust Network Topology

```mermaid
flowchart TD
    subgraph LocalDev ["Local Developer Workstation"]
        ADS["Azure Data Studio / SSMS"]
        DEV_CLI["Azure CLI (Dynamic Firewall Rule)"]
        DEV_APP["dotnet run (User Secrets)"]
    end

    subgraph AzureCloud ["Microsoft Azure Cloud Perimeter"]
        subgraph VNet ["Virtual Network: vnet-dietdost-dev (10.0.0.0/16)"]
            subgraph Subnet ["Delegated Subnet: snet-aca-infra (10.0.0.0/23)"]
                ACA["Azure Container Apps: app-dietdost-web-dev"]
                SVC_EP["Service Endpoint: Microsoft.Sql"]
            end
            NSG["NSG: Allow-SQL-Outbound (Port 1433)"]
        end

        subgraph SQLBoundary ["Azure SQL Logical Server (sql-dietdost-dev)"]
            VNET_RULE["VNet Rule: allow-aca-subnet"]
            FW_RULE["Firewall Rule: LocalDevWorkstationIP"]
            DB[("dietdost (GP_S_Gen5_1 Serverless Free)")]
        end

        subgraph SecretVault ["Azure Key Vault (kvdostdev)"]
            SEC_PWD["Secret: sql-admin-password"]
            SEC_CONN["Secret: azure-sql-connection-string"]
        end
    end

    ACA -->|Private Backbone via Service Endpoint| VNET_RULE
    VNET_RULE --> DB
    ACA -->|Managed Identity (Zero Password in Config)| SEC_CONN
    DEV_CLI -.->|az sql server firewall-rule| FW_RULE
    ADS -.->|Port 1433 Encrypted TLS 1.2| FW_RULE
    FW_RULE --> DB
    DEV_APP -.->|User Secrets Connection String| FW_RULE
```

### Authentication Standards
- **SQL Server Authentication**: Azure AD / Entra ID authentication cannot be used due to enterprise directory permission constraints in the subscription. Deployments utilize SQL Authentication with login `sqladmin`.
- **Zero Plaintext Secrets**: The database administrator password is **never committed to Git or stored in plaintext config files**.
  - Generated/injected during infrastructure deployment via GitHub Secrets (`secrets.AZURE_SQL_ADMIN_PASSWORD`) or interactive CLI prompt.
  - Automatically saved to Azure Key Vault under secrets `sql-admin-password` and `azure-sql-connection-string`.
  - Injected into Azure Container Apps at runtime via User-Assigned Managed Identity secret references (`secretRef: 'azure-sql-connection-string'`).

### Network Perimeter Standards
- **Private Azure Backbone (Service Endpoints)**: Container Apps communicate with Azure SQL through the `Microsoft.Sql` service endpoint on `snet-aca-infra` linked via `Microsoft.Sql/servers/virtualNetworkRules`. Traffic never leaves Microsoft's software-defined backbone.
- **Egress NSG Rule**: `nsg-dietdost-dev` includes an explicit outbound rule `Allow-SQL-Outbound` permitting TCP traffic on ports **1433** (gateway) and **11000–11999** (database cluster worker nodes) to destination service tag `Sql` with priority 105. This satisfies Azure SQL's internal **Redirect connection policy** without dropping worker node data-plane packets.

---

## 3. Infrastructure as Code (Bicep) Architecture

| File | Subsystem Role | Key Parameters & Safe Defaults |
| :--- | :--- | :--- |
| [`infra/dev/sql.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/sql.bicep) | Dedicated SQL Serverless Free Tier Module | Provisions logical server, `GP_S_Gen5_1` free database (`useFreeLimit: true`), VNet rule, firewall rules, and Key Vault secrets. |
| [`infra/dev/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/infra.bicep) | Foundation Infrastructure | Adds `Microsoft.Sql` service endpoint to `acaSubnet`, `Allow-SQL-Outbound` NSG rule, and conditionally invokes `sql.bicep` when `deployAzureSql == true`. |
| [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep) | Workload Container App | Maps Key Vault secret reference `azure-sql-connection-string` into `ConnectionStrings__AzureSqlConnection`. |
| [`infra/dev/main.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/main.bicep) | Composite Deployment | Default `deployAzureSql = false`. Requires explicit parameter override to enable. |

---

## 4. Cloud Deployment Runbooks

### Method A: Automated Deployment via GitHub Actions (Recommended)

1. **Prerequisite**: Ensure GitHub Repository Secret `AZURE_SQL_ADMIN_PASSWORD` is configured in repository settings.
2. Navigate to **Actions** $\to$ **Aspire - Provision Azure Infrastructure** (`azure-infra-deploy.yml`).
3. Click **Run workflow** and configure inputs:
   - **Target environment**: `dev`
   - **Deploy Azure SQL Database Serverless Free Tier**: Check `true` (default: `false`).
   - **SQL Server Administrator login username**: `sqladmin`
   - **Optional developer workstation client IP**: (Optional: provide public IP for immediate workstation access).
4. Run the workflow. Upon completion:
   - SQL Server and Free Database are provisioned.
   - VNet rules and Service Endpoints are active.
   - Key Vault secret `azure-sql-connection-string` is written.
5. Run **Aspire - Deploy Application Workload** (`azure-app-deploy.yml`) with:
   - **Persistence database provider**: `AzureSql` (default: `Sqlite`).

### Method B: Manual Deployment via Azure CLI

```powershell
# 1. Login to Azure and select subscription
az login
az account set --subscription "<Your-Subscription-Id>"

# 2. Deploy Foundation Infrastructure with Azure SQL enabled
# Note: When deployAzureSql=true and sqlAdminPassword is omitted, Azure CLI prompts interactively.
az deployment group create `
  --resource-group rg-dietdost-dev `
  --template-file infra/dev/infra.bicep `
  --parameters environment=dev `
               deployAzureSql=true `
               sqlAdminLogin="sqladmin" `
               sqlAdminPassword="<YourStrongPasswordHere>" `
               allowedClientIp="(Invoke-RestMethod https://api.ipify.org)"
```

---

## 5. Local Workstation Access Runbook

When developing or debugging locally, follow these steps to connect to the deployed Azure SQL database:

### Step 1: Punch Client IP into Azure SQL Server Firewall
Azure SQL blocks all public internet access by default. Run this PowerShell snippet from your workstation:

```powershell
# 1. Fetch workstation's current public IP address
$myIp = (Invoke-RestMethod https://api.ipify.org).Trim()
Write-Host "Workstation Public IP: $myIp"

# 2. Add dynamic firewall rule
az sql server firewall-rule create `
  --resource-group rg-dietdost-dev `
  --server sql-dietdost-dev-<uniqueSuffix> `
  --name "Workstation-$(Get-Date -Format 'yyyyMMdd')" `
  --start-ip-address $myIp `
  --end-ip-address $myIp
```

### Step 2: Retrieve the Secure Connection String from Key Vault
```powershell
$connStr = az keyvault secret show `
  --vault-name kvdostdev<uniqueSuffix> `
  --name "azure-sql-connection-string" `
  --query value -o tsv
```

### Step 3: Configure Local Application via .NET User Secrets
```powershell
cd src/Nutrition.WebGateway
dotnet user-secrets set "Database:Provider" "AzureSql"
dotnet user-secrets set "ConnectionStrings:AzureSqlConnection" "$connStr"
```

To switch back to local zero-cost SQLite:
```powershell
dotnet user-secrets set "Database:Provider" "Sqlite"
```

### Step 4: Connecting via SQL Management Tools (Azure Data Studio / SSMS / VS Code MSSQL)
- **Server**: `sql-dietdost-dev-<uniqueSuffix>.database.windows.net,1433`
- **Database**: `dietdost`
- **Authentication**: `SQL Server Authentication`
- **Username**: `sqladmin`
- **Password**: *(retrieved from Key Vault secret `sql-admin-password`)*
- **Encryption**: `Mandatory` (`Trust Server Certificate: False`)
- **Connection Timeout**: `60` seconds *(critical for cold-start auto-pause resumes)*

---

## 6. Cold-Start Wake-Up & Transient Handling (Error 40613)

### The Cold-Start Challenge
When compute is paused after 60 minutes of inactivity, incoming connection attempts cause Azure SQL to resume compute. Resuming compute takes between **15 and 45 seconds**. During this resumption window, incoming connections receive SQL error code **40613**:
> *"Database 'dietdost' on server '...' is not currently available. Please retry the connection later."*

### Application Safeguards
1. **60-Second Connection Timeout**: ADO.NET defaults to 15 seconds, which causes socket timeout before resumption finishes. `StorageInfrastructureExtensions` automatically inspects and enforces a minimum **60-second `ConnectTimeout`**.
2. **Execution Retry Strategy**: EF Core is configured with:
   ```csharp
   sqlOpts.EnableRetryOnFailure(
       maxRetryCount: 6,
       maxRetryDelay: TimeSpan.FromSeconds(30),
       errorNumbersToAdd: new[] { 40613 });
   sqlOpts.CommandTimeout(60);
   ```
3. **Container App Health Probe Tolerance**: ACA startup and readiness probes (`/healthz` and `/ready`) in `app.bicep` are configured with a failure threshold of 30 checks $\times$ 5 seconds = 150 seconds, preventing ACA from terminating the container while the database wakes up.

---

## 7. Multi-Tier Seeding & Functional Parity

When running against Azure SQL, [`DatabaseInitializationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs) guarantees:
- **Pristine Bootstrap**: On an empty database, EF Core `EnsureCreatedAsync()` creates all 12 model tables natively with primary keys, indexes, and T-SQL types.
- **Incremental Reconciliation**: If tables already exist, `INFORMATION_SCHEMA.TABLES` and `INFORMATION_SCHEMA.COLUMNS` verify and safely add any missing tables or columns.
- **5-Tier Demo Seeding**: Deterministically seeds accounts for all tiers:
  - `free@dietdost.app` (Free tier limits)
  - `basic@dietdost.app` (Basic tier limits)
  - `premium@dietdost.app` (Premium photo comparison & exports)
  - `admin.demo@dietdost.app` (Admin portal inspection)
  - `superadmin@dietdost.app` (Root system governance)
  - Password: `DietDost@Demo2026!`
- **Cross-Platform Parity**: Web PWA, Android, and iOS clients operate against identical REST endpoints with zero platform-specific divergence.

---

## 8. Migrating Data from Local SQLite to Azure SQL

Diet-Dost provides an automated .NET 11 CLI tool (`Nutrition.Tools.DataMigrator`) and PowerShell wrapper (`scripts/migrate-sqlite-to-azuresql.ps1`) to copy data from a local SQLite database (`.db`) to Azure SQL Database.

### 8.1 Key Features
- **Automatic Data Type Conversions**:
  - Timestamps normalized to universal UTC `DateTime` and clamped to valid `DATETIME2` ranges.
  - SQLite integer/text booleans converted to SQL Server `BIT`.
  - JSON collection columns (`DiagnosedConditions`, `Medications`, `WhoComplianceFlags`, `MedicationWarnings`, `EarnedBadges`) validated and repaired to prevent EF Core deserialization errors.
  - Invariant float parsing with NaN / Infinity handling.
  - String GUID preservation maintaining all foreign key relationships across users, meals, food items, and ledgers.
- **Foreign Key Constraint Safety**: Temporarily deactivates foreign key constraints during batch ingestion (`NOCHECK CONSTRAINT ALL`) and re-enables and validates integrity after (`WITH CHECK CHECK CONSTRAINT ALL`).
- **Conflict Strategies**:
  - `Upsert` (Default): Updates existing records by Primary Key, inserts new records.
  - `CleanInsert`: Clears target table before inserting.
  - `SkipExisting`: Inserts new records only, preserving existing Azure SQL records.
- **Reporting**: Prints rich per-table metrics in the terminal and generates a timestamped Markdown report in `docs/migration-reports/migration_report_<timestamp>.md`.

### 8.2 Execution Examples

#### 1. Dry-Run Validation (Zero Write / Safe Preview)
```powershell
pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 `
  -SqliteDbPath "src/Nutrition.WebGateway/diettracker.db" `
  -DryRun
```

#### 2. Live Migration with Auto-Retrieved Connection String
If logged into Azure CLI, the script automatically retrieves the connection string from Azure Key Vault:
```powershell
pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 `
  -SqliteDbPath "C:\path\to\my_local.db" `
  -Mode Upsert `
  -EnsureSchema
```

#### 3. Direct .NET 11 CLI Command
```powershell
dotnet run --project src/Nutrition.Tools.DataMigrator -- `
  --sqlite "src/Nutrition.WebGateway/diettracker.db" `
  --azure-sql "$env:AZURE_SQL_CONNECTION_STRING" `
  --mode Upsert `
  --ensure-schema
```


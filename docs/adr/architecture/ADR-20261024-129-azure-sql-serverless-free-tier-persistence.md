<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-129: Azure SQL Serverless Free Tier Persistence & Phased NoSQL Architecture

## Status
Accepted & Fully Implemented (Bicep IaC + SQL Server Authentication + VNet Service Endpoints + Resilient Schema Migration)

## Context
Diet-Dost initially utilized an embedded SQLite database (`diettracker.db`) with an SMB volume mount for fast MVP validation. While SQLite is optimal for local development, fast unit tests, and cross-platform mobile client caching, multi-user cloud deployments require horizontal scalability, managed ACID guarantees, enterprise compliance (DPDPA 2023, TDE), and zero cloud hosting cost for the startup phase.

Microsoft Azure provides a permanent **Azure SQL Database Serverless Free Tier** granting **100,000 vCore-seconds and 32 GB storage completely free every month for the lifetime of the Azure subscription**. Additionally, future roadmap requirements anticipate a hybrid polyglot architecture where dynamic meal logs and multimodal AI vision metadata may be offloaded to Azure Cosmos DB Free Tier (1,000 RU/s + 25 GB free forever).

### Authentication & Networking Requirements
- **Authentication Constraint**: Azure AD / Entra ID authentication cannot be used due to directory permission constraints in the subscription. Deployment must use **SQL Server Authentication** with credentials securely generated and stored in Azure Key Vault (`sql-admin-password` and `azure-sql-connection-string`).
- **Network Perimeter**: When deployed, traffic between Azure Container Apps (ACA) and Azure SQL must not traverse the public internet. Connectivity must use a **Virtual Network Service Endpoint** (`Microsoft.Sql` on `snet-aca-infra`) bound to the SQL Server via `Microsoft.Sql/servers/virtualNetworkRules`.
- **Developer Local Machine Access**: Developers need clear, reproducible mechanisms to connect to the deployed Azure SQL database from their local machines (via Azure CLI firewall punches and user secrets).

## Decision
1. **Multi-Provider Persistence Router**:
   - `StorageInfrastructureExtensions.AddStorageInfrastructure` supports `Provider = "AzureSql"` / `"SqlServer"` alongside `"Sqlite"`.
   - Uses `Microsoft.EntityFrameworkCore.SqlServer` (`11.0.0-rc.1.26425.128`) targeting .NET 11 with pinned `System.Security.Cryptography.Pkcs` (`9.0.21`) for ASVS vulnerability compliance.
   - Enforces a minimum **60-second connection timeout** and **60-second command timeout** to survive Serverless auto-pause cold starts (15-45s wake-up latency).
   - Configures `EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: new[] { 40613 })` to retry on transient resume error 40613.

2. **Infrastructure as Code (IaC) - Azure SQL Serverless Free Tier Module (`infra/dev/sql.bicep`)**:
   - Logical Server: `Microsoft.Sql/servers@2023-08-01-preview` with SQL Administrator authentication and TLS 1.2 minimal version.
   - Serverless Free Database: `Microsoft.Sql/servers/databases@2023-08-01-preview` with SKU `GP_S_Gen5_1`, `useFreeLimit: true`, `freeLimitExhaustionBehavior: 'AutoPause'`, `autoPauseDelay: 60`, `minCapacity: 0.5`, and `maxSizeBytes: 34359738368` (32 GB).
   - Service Endpoint & VNet Rule: `Microsoft.Network/virtualNetworks/subnets` configures `Microsoft.Sql` service endpoint on `snet-aca-infra`, and `Microsoft.Sql/servers/virtualNetworkRules` binds the subnet to the server.
   - Network Security Group (NSG): Outbound rule `Allow-SQL-Outbound` permits TCP port 1433 to destination `Sql`.
   - Key Vault Integration: Automatically provisions secrets `sql-admin-password` and `azure-sql-connection-string`.
   - Container App Secret Wiring (`app.bicep`): Secret reference `azure-sql-connection-string` maps to container environment variable `ConnectionStrings__AzureSqlConnection`.

3. **Provider-Agnostic Database Migration & Schema Reconciliation**:
   - In `DatabaseInitializationExtensions.cs`:
     - Clean separation of SQLite vs SQL Server migration paths.
     - For SQL Server: calls `db.Database.EnsureCreatedAsync()` on pristine empty databases to create all 12 model tables natively.
     - Incremental table and column reconciler queries `INFORMATION_SCHEMA.TABLES` and `INFORMATION_SCHEMA.COLUMNS`, adding missing tables (`Corrections`, `ProgressPhotos`, `AiFeedbacks`, `Users`, `VerificationOtps`, `TierConfigurations`, `AiUsageLogs`, `AppSecrets`) and columns (`TotalFiberGrams`, `TotalSugarGrams`, etc.) using valid T-SQL.
     - All 5 user tiers (`Free`, `Basic`, `Premium`, `Admin`, `SuperAdmin`) seed cleanly on SQL Server.

4. **Local Workstation Access Guide**:
   - Step 1: Punch client IP firewall rule:
     ```bash
     $clientIp = (Invoke-RestMethod https://api.ipify.org)
     az sql server firewall-rule create --resource-group rg-dietdost-dev --server <sql-server-name> --name LocalDevIP --start-ip-address $clientIp --end-ip-address $clientIp
     ```
   - Step 2: Configure local user-secrets:
     ```bash
     dotnet user-secrets set "ConnectionStrings:AzureSqlConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=dietdost;User ID=sqladmin;Password=<pwd>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
     dotnet user-secrets set "Database:Provider" "AzureSql"
     ```

## Consequences
- **Positive**:
  - $0.00/month cloud bill on Microsoft Azure leveraging the official 100,000 vCore-s + 32 GB free grant.
  - Zero disruption to local development: SQLite remains the default for developers and unit test suites.
  - Full end-to-end support across all 5 tiers (`Free`, `Basic`, `Premium`, `Admin`, `SuperAdmin`) and all platforms (Web PWA, Android, iOS).
  - High security: zero plaintext credentials in source control; connection strings encrypted and secured in Key Vault.
  - Clean networking: container apps communicate directly over Microsoft's backbone via Service Endpoint without traversing public internet.
- **Negative / Considerations**:
  - Azure SQL Serverless pauses after 60 minutes of inactivity; the initial connection takes 20-40 seconds to resume. Handled via EF Core execution retry strategy, Polly backoff, and 60-second connection timeout.

## Verification
- Unit & Persistence Tests: `tests/Nutrition.EvalHarness.Tests/AzureSqlPersistenceTests.cs` (100% pass rate).
- Bicep IaC Compilation: `az bicep build --file infra/dev/main.bicep` (0 errors, 0 warnings).
- Multi-tier seeding verified for both SQLite and SQL Server schemas.

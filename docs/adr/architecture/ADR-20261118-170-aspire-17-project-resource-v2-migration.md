<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261118-170: Aspire 17 and Project Resource v2 Migration with Dual-Database Orchestration

- **Status**: Accepted
- **Date**: 2026-11-18
- **Domain**: Architecture / DevOps / Persistence
- **Deciders**: Diet-Dost Engineering, Cloud DevOps & Distributed Architecture Team
- **Tracking Issue**: #167

---

## 1. Context & Problem Statement

Prior to this architectural migration, the .NET Aspire orchestrator project (`Nutrition.AppHost`) relied on the legacy MSBuild-coupled orchestration model:
1. **Coupled MSBuild Build Graph**: `Nutrition.AppHost.csproj` declared static MSBuild project references (`<ProjectReference Include="..\Nutrition.WebGateway\Nutrition.WebGateway.csproj" />`). When building or inspecting the AppHost, MSBuild was forced to evaluate and build the entire transitive dependency tree of child web gateways, application CQRS handlers, domain models, and third-party dependencies.
2. **Deprecated Project SDK**: The AppHost used `<Project Sdk="Aspire.AppHost.Sdk/13.5.4">` (or `13.6.5`), which generates MSBuild warning `ASPIRE012: The Aspire.AppHost.Sdk SDK is obsolete. Use the Microsoft.NET.Sdk with PackageReference to Aspire.Hosting.AppHost instead.`
3. **Monolithic ProjectResource Model**: In previous Aspire versions, project orchestration depended on compile-time code generation (`Projects.Nutrition_WebGateway`) under `IResourceBuilder<ProjectResource>`. This impeded polyglot orchestration and prevented dynamic project substitution.
4. **Hardcoded Database Persistence in Orchestrator**: The AppHost extension `WebGatewayResourceExtensions.cs` hardcoded `Database__Provider = "Sqlite"` and `"Data Source=diettracker.db"`, preventing seamless orchestration and manifest generation for Azure SQL / SQL Server environments.

---

## 2. Decision & Technical Implementation

We migrated `Nutrition.AppHost` to **.NET Aspire 17** utilizing **Project Resource v2 (`DotnetProjectResource`)** and parameterized dual-database persistence.

### 2.1 Decoupled Standard SDK & Aspire 17 Packages
- Migrated `src/Nutrition.AppHost/Nutrition.AppHost.csproj` to `<Project Sdk="Microsoft.NET.Sdk">`.
- Removed all transitive `<ProjectReference>` elements, completely severing the MSBuild compilation graph between AppHost and child services.
- Added official Aspire 17.0.0 packages:
  - `Aspire.Hosting.AppHost` (17.0.0)
  - `Aspire.Hosting.Dotnet` (17.0.0) — introduces `AddDotnetProject` and `DotnetProjectResource`
  - `Aspire.Hosting.Azure.AppContainers` (17.0.0)
  - `Aspire.Hosting.Azure.KeyVault` (17.0.0)
  - `Aspire.Hosting.Azure.Sql` (17.0.0) — provides first-class Azure SQL server & database orchestration
  - `Aspire.Hosting.Azure.Storage` (17.0.0)
- Updated global tool `aspire.cli` to `17.0.0`.

### 2.2 Project Resource v2 Implementation (`DotnetProjectResource`)
- Updated `src/Nutrition.AppHost/Extensions/WebGatewayResourceExtensions.cs` to leverage `builder.AddDotnetProject(ResourceName, "../Nutrition.WebGateway/Nutrition.WebGateway.csproj")`.
- Replaced the legacy return type `IResourceBuilder<ProjectResource>` with `IResourceBuilder<DotnetProjectResource>`.
- `DotnetProjectResource` models child projects as execution-level resources (`ExecutableResource`) rather than MSBuild build-time dependencies, building projects on demand through Aspire's background build subsystem (`obj\.aspire\build\projects.*.proj`).

### 2.3 Dynamic Dual-Database Persistence Orchestration
- Parameterized database provider detection in `WebGatewayResourceExtensions.cs`:
  - Dynamically reads `Database:Provider` (or env `Database__Provider`) defaulting to `Sqlite`.
  - Dynamically forwards `ConnectionStrings:DefaultConnection` for SQLite.
  - Dynamically forwards `ConnectionStrings:AzureSqlConnection` when configured.
  - Added optional `IResourceBuilder<IResourceWithConnectionString>? database = null` parameter, enabling native `.WithReference(database)` wiring when Azure SQL container or cloud resources are declared in AppHost.

```csharp
public static IResourceBuilder<DotnetProjectResource> AddWebGateway(
    this IDistributedApplicationBuilder builder,
    IResourceBuilder<AzureKeyVaultResource>? keyVault = null,
    IResourceBuilder<IResourceWithConnectionString>? database = null)
{
    var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
    var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";
    var azureSqlConn = builder.Configuration.GetConnectionString("AzureSqlConnection");

    var webGateway = builder.AddDotnetProject(ResourceName, "../Nutrition.WebGateway/Nutrition.WebGateway.csproj")
        .WithHttpEndpoint(port: GatewayPort, isProxied: false)
        .WithExternalHttpEndpoints()
        .WithEnvironment("Database__Provider", dbProvider)
        .WithEnvironment("ConnectionStrings__DefaultConnection", defaultConn);

    if (!string.IsNullOrWhiteSpace(azureSqlConn))
    {
        webGateway.WithEnvironment("ConnectionStrings__AzureSqlConnection", azureSqlConn);
    }

    if (database != null) webGateway.WithReference(database);
    if (keyVault != null) webGateway.WithReference(keyVault);

    return webGateway;
}
```

---

## 3. Verification & Validation Evidence

1. **Isolated AppHost Decoupled Build**:
   - `dotnet build src/Nutrition.AppHost/Nutrition.AppHost.csproj` completed in 0.9s with 0 warnings and 0 errors.
   - Verified that `Nutrition.WebGateway`, `Nutrition.Application`, and `Nutrition.Domain` are NOT built when compiling `AppHost`.
2. **Full Solution Build**:
   - `dotnet build DietDost.slnx` built all 6 projects cleanly (0 warnings, 0 errors).
3. **Aspire Manifest Synthesis (`aspire publish`)**:
   - `aspire publish --apphost src/Nutrition.AppHost --output-path ./aspire-output --non-interactive` executed 11/11 pipeline steps in 0.17s, successfully generating all Bicep IaC modules (`cae-dietdost-acr`, `cae-dietdost`, `dietdost-storage`, `dietdost-kv`, `web-gateway`).
4. **Dual Database Persistence Verification**:
   - **SQLite**: Local `Nutrition.WebGateway` runtime executed against `diettracker.db`. Passed 100% of the live multi-tier CFT suite (`validate_e2e_tiers.ps1`) across all 5 user tiers (Free, Basic, Premium, Admin, SuperAdmin).
   - **Azure SQL**: Executed `AzureSqlPersistenceTests` (11/11 passed) and `DualDatabaseSchemaMigrationTests` (2/2 passed) confirming SQL Server provider resolution, 60s/120s auto-pause timeout guards, and retrying execution strategies.
5. **Full Automated Test Suite**:
   - Executed `dotnet test`: 488 tests passed, 0 failed across `Nutrition.Domain.Tests` and `Nutrition.EvalHarness.Tests`.

---

## 4. Consequences & Impact

### Positive
- **Instantaneous AppHost Builds**: AppHost builds independently in sub-second times without triggering recompilation of domain, application, or web projects.
- **Alignment with .NET Aspire Roadmap**: Eliminates obsolete `Aspire.AppHost.Sdk` (`ASPIRE012`) and positions the solution for full Aspire 17 polyglot microservice orchestrations.
- **Dual-Database Portability**: Facilitates seamless switching between local SQLite development and Azure SQL cloud production.

### Neutral / Operational
- Developers using Aspire CLI require `aspire.cli` version `17.0.0+` (`dotnet tool update -g aspire.cli`).

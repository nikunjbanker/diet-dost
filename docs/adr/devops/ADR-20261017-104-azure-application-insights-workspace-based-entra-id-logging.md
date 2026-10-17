<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261017-104: Workspace-Based Azure Application Insights with Local Authentication Disabled and Entra ID Logs-Only Routing

## Status
**Accepted** (2026-10-17)

## Context
1. **Application Observability Gap**:
   - The foundation cloud infrastructure defined an Azure Log Analytics Workspace (`log-dietdost-dev`), but lacked a dedicated Azure Application Insights component.
   - Container Apps environment console logs captured stdout, but application structured logging, exception stack traces, and OpenTelemetry diagnostic telemetry from `Nutrition.WebGateway` were not ingested into an indexed application performance management (APM) interface.
2. **Strict Security Posture (Zero Local Auth)**:
   - Modern enterprise security benchmarks (CIS Microsoft Azure Foundations Benchmark and Checkov `CKV_AZURE_224`) require that Application Insights resources strictly disable local authentication (`DisableLocalAuth: true`).
   - Using static API keys or unauthenticated `InstrumentationKey` ingestion introduces credential leakage risks and violates Zero-Trust principles.
3. **Telemetry Separation Constraint (Logs Only)**:
   - To preserve high performance and minimize telemetry ingestion costs, distributed traces (spans) and custom application metrics must remain exclusively on the local .NET Aspire OTLP collector.
   - Only `ILogger` structured logs and unhandled application exceptions must route to Azure Application Insights.

## Decision
1. **Workspace-Based Application Insights Provisioning**:
   - In [`infra/dev/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/infra.bicep), provisioned a workspace-based Application Insights resource (`appi-dietdost-${environment}`) of type `Microsoft.Insights/components@2020-02-02` linked to `logAnalytics.id` via `WorkspaceResourceId: logAnalytics.id`.
   - Explicitly configured `DisableLocalAuth: true`, enforcing Entra ID authentication and permanently disabling legacy API keys and InstrumentationKey-only authentication.
2. **Entra ID RBAC Role Delegation**:
   - Assigned the built-in **`Monitoring Metrics Publisher`** role (`39135dd6-16b0-4780-a6e6-8577137d9569`) to the User-Assigned Managed Identity (`id-dietdost-${environment}`) scoped directly to the Application Insights resource:
     ```bicep
     resource appiMonitoringPublisherRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
       name: guid(appInsights.id, managedIdentity.id, '39135dd6-16b0-4780-a6e6-8577137d9569')
       scope: appInsights
       properties: {
         roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '39135dd6-16b0-4780-a6e6-8577137d9569')
         principalId: managedIdentity.properties.principalId
         principalType: 'ServicePrincipal'
       }
     }
     ```
3. **Application Telemetry Configuration (Logs Only)**:
   - Installed `Azure.Monitor.OpenTelemetry.AspNetCore 1.6.0` in [`src/Nutrition.WebGateway/Nutrition.WebGateway.csproj`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Nutrition.WebGateway.csproj).
   - In [`src/Nutrition.WebGateway/Extensions/OpenTelemetryExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/OpenTelemetryExtensions.cs), configured `DefaultAzureCredential` with `ManagedIdentityClientId: AZURE_CLIENT_ID`.
   - Wired `otLogging.AddAzureMonitorLogExporter(...)` strictly under `builder.Logging.AddOpenTelemetry(...)`.
   - Did NOT call `UseAzureMonitor()`, preserving the boundary so that trace spans and metrics remain exclusively on the Aspire OTLP pipeline.
4. **End-to-End Pipeline & Workload Integration**:
   - In [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep), added `@secure() param appInsightsConnectionString string = ''`, registered `app-insights-connection-string` secret in ACA configuration, and injected `APPLICATIONINSIGHTS_CONNECTION_STRING` into the container environment.
   - In [`infra/dev/main.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/main.bicep), connected `foundation.outputs.appInsightsConnectionString` to the `workload` module and exported `appInsightsName`, `appInsightsId`, and `appInsightsConnectionString`.
   - In [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml) and [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml), resolved Application Insights outputs and passed them seamlessly to container deployments.

## Consequences
- **Positive**:
  - Full application log observability and exception diagnostics in Azure Portal via Application Insights and Log Analytics.
  - Zero local authentication credentials or static keys; 100% compliant with Checkov `CKV_AZURE_224` and CIS Azure benchmarks.
  - Telemetry isolation guaranteed: traces and metrics stay on Aspire OTLP, preventing unnecessary cloud ingestion billing.
- **Negative / Neutral**:
  - Telemetry ingestion under `DisableLocalAuth: true` requires valid Entra ID tokens; local development falls back gracefully to standard console and Aspire OTLP when no Azure credential or connection string is supplied.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-106: Azure Application Insights Monitoring Metrics Publisher Role Definition GUID Remediation

## Status
**Accepted** (2026-10-18)

## Context
1. **GitHub Actions Infrastructure Provisioning Failure**:
   - In GitHub Actions run `39065997388` during the `provision-infrastructure` stage of Azure deployment, the ARM Bicep deployment failed with error:
     ```json
     {
       "code": "RoleDefinitionDoesNotExist",
       "message": "The specified role definition with ID '39135dd616b04780a6e68577137d9569' does not exist."
     }
     ```
2. **Root Cause Analysis (Invalid Role Definition GUID)**:
   - In [`infra/dev/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/infra.bicep), the resource `appiMonitoringPublisherRole` intended to assign the official Azure built-in role **Monitoring Metrics Publisher** to the User-Assigned Managed Identity (`id-dietdost-${environment}`) scoped to Application Insights.
   - However, the Bicep template referenced a typographical/non-existent GUID: `39135dd6-16b0-4780-a6e6-8577137d9569`.
   - The authoritative Azure built-in role ID for **Monitoring Metrics Publisher** is `3913510d-42f4-4e42-8a64-420c390055eb` (`/providers/Microsoft.Authorization/roleDefinitions/3913510d-42f4-4e42-8a64-420c390055eb`).

## Decision
1. **Remediate Role Definition GUID**:
   - Updated `infra/dev/infra.bicep` to use the canonical Azure built-in role ID `3913510d-42f4-4e42-8a64-420c390055eb` for `appiMonitoringPublisherRole`:
     ```bicep
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
     ```
2. **Synchronize Automated Contract Tests & Architecture Documentation**:
   - Updated [`tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs) to assert `3913510d-42f4-4e42-8a64-420c390055eb`.
   - Synchronized [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md) and [`docs/adr/devops/ADR-20261017-104-azure-application-insights-workspace-based-entra-id-logging.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261017-104-azure-application-insights-workspace-based-entra-id-logging.md).

## Consequences
- **Positive**:
  - Eliminates the `RoleDefinitionDoesNotExist` ARM deployment blocker.
  - Successfully grants the Managed Identity the exact permissions required to ingest logs into Application Insights under `DisableLocalAuth: true`.
- **Negative / Neutral**:
  - None; purely a corrective bug fix aligning the Bicep template with the authoritative Azure RBAC role catalog.

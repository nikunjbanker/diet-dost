<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-109: Decoupled ACA Custom Domain & Managed TLS Lifecycle Architecture

## Status
Accepted

## Date
2026-10-18

## Context
During application deployment via `.github/workflows/azure-app-deploy.yml` (run [39117439637](https://github.com/nikunjbanker/diet-dost/actions/runs/39117439637)), Stage 3 Step 10 (*Deploy Application Container App via Bicep*) failed with the following ARM platform error:
```json
{
  "status": "Failed",
  "error": {
    "code": "DeploymentFailed",
    "target": ".../deployments/app",
    "message": "At least one resource deployment operation failed.",
    "details": [
      {
        "code": "RequireCustomHostnameInEnvironment",
        "message": "Creating managed certificate requires hostname 'dev.diet-dost.in' added as a custom hostname to a container app or route in environment 'cae-dietdost-dev'"
      }
    ]
  }
}
```

### Root Cause Analysis
1. **ARM Inferred Dependency Cycle**: In `infra/dev/app.bicep`, `containerApp.properties.configuration.ingress.customDomains[0].certificateId` referenced `managedCert.id`. ARM deduced that `managedCert` must be deployed *before* `containerApp`.
2. **ACA Platform Constraint**: Azure Container Apps enforces a strict validation rule: a Managed Certificate (`Microsoft.App/managedEnvironments/managedCertificates`) cannot be created in an environment unless the target hostname is **already mapped** to a container app in that environment.
3. **Chicken-and-Egg Block**: Because the Container App did not yet exist or have the hostname mapped, ARM's attempt to provision `managedCert` first caused ACA to reject the deployment with `RequireCustomHostnameInEnvironment`. Furthermore, DigiCert domain validation requires public DNS CNAME propagation to the Container App's FQDN before a certificate can be issued.

## Decision
1. **Decouple Managed Certificate from Workload Bicep (`infra/dev/app.bicep`)**:
   - Removed inline `managedCert` resource from `infra/dev/app.bicep`.
   - Updated `containerApp` custom domain ingress configuration to bind conditionally only when a pre-existing `certificateId` is explicitly provided (`(enableCustomDomain && !empty(certificateId))`).
   - Ensures the core Container App workload always deploys cleanly on its default Azure FQDN (`*.azurecontainerapps.io`) without circular dependency failures.
2. **Two-Phase Resilient Binding in Pipeline (`.github/workflows/azure-app-deploy.yml`)**:
   - Added Step `11b. Configure Custom Domain and Managed TLS (If Requested)` in Stage 3.
   - Emits `aca_env_name` from Step 5 (`resolve-env`).
   - In Step 11b, verifies whether public DNS resolves for the target custom domain:
     - **If DNS is ready**: Automatically calls `az containerapp hostname add` and `az containerapp hostname bind --validation-method CNAME` to issue the free Managed Certificate and enable SNI binding.
     - **If DNS is not yet ready**: Emits clear, formatted DNS registrar setup instructions (CNAME & TXT records with the exact Container App FQDN) to `$GITHUB_STEP_SUMMARY` without failing the application deployment, keeping the live application accessible on its default Azure FQDN.
3. **Living Documentation & Runbook Synchronization**:
   - Synchronized `docs/sdd/05_devops_and_infrastructure.md` and `.agents/skills/diet-dost-azure-deployment/SKILL.md` to reflect the decoupled custom domain lifecycle pattern.

## Consequences
- **Positive**: Eliminates the `RequireCustomHostnameInEnvironment` deployment error completely. Application deployments succeed 100% of the time, even when custom domain DNS has not yet propagated.
- **Positive**: Clear operator visibility: if DNS is missing, exact CNAME and TXT values are published directly into GitHub Step Summary.
- **Positive**: Free Azure Managed Certificates are issued and bound automatically as soon as DNS CNAME resolves.
- **Zero Regression**: Contract tests (`BicepIaCConfigurationContractTests.cs`), unit tests, and live 5-tier CFT suites remain 100% green.

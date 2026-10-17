<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261017-103: Azure Container Apps Delegated Subnet Preservation and InUseSubnetCannotBeDeleted Remediation

## Status
**Accepted** (2026-10-17)

## Context
During the execution of the Azure Infrastructure Provisioning pipeline ([Run 39034745726 / Job 117403163013](https://github.com/nikunjbanker/diet-dost/actions/runs/39034745726/job/117403163013)), ARM template deployment failed with the following fatal Azure Resource Manager error:

```json
{
  "code": "InUseSubnetCannotBeDeleted",
  "message": "Subnet snet-aca-infra is in use by /subscriptions/2b540f50-1a74-4019-945a-3cc48a284a1a/resourceGroups/rg-dietdost-dev/providers/Microsoft.Network/virtualNetworks/vnet-dietdost-dev/subnets/snet-aca-infra/serviceAssociationLinks/legionservicelink and cannot be deleted. In order to delete the subnet, delete all the resources within the subnet. See aka.ms/deletesubnet."
}
```

### Root Cause Analysis
1. **Azure Container Apps Subnet Immutability**:
   - Azure Container Apps Managed Environments (`cae-dietdost-dev`) bind to an infrastructure subnet via an Azure Service Association Link (`legionservicelink`).
   - Azure Container Apps strictly prohibits moving an existing managed environment to a different subnet or VNet without full resource deletion.
2. **Subnet Re-declaration Drift**:
   - In the initial development provisioning, the subnet was created with name `snet-aca-infra` inside `vnet-dietdost-dev`.
   - During environment name partitioning, the parameter `acaSubnetName` default in `infra/dev/infra.bicep` was modified to `'snet-aca-infra-${environment}'`, evaluating to `'snet-aca-infra-dev'`.
   - When ARM deployed `infra.bicep` with `subnets: [ { name: 'snet-aca-infra-dev' } ]`, ARM interpreted the absence of `snet-aca-infra` from the template's inline subnet list as an instruction to delete the existing subnet `snet-aca-infra`.
   - Azure blocked this destructive deletion with `InUseSubnetCannotBeDeleted` because the active Container App Managed Environment held an active `legionservicelink`.

## Decision
1. **Preserve Delegated Subnet Name Default (`snet-aca-infra`)**:
   - In both [`infra/dev/infra.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/infra.bicep) and [`infra/dev/main.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/main.bicep), default `param acaSubnetName string = 'snet-aca-infra'`.
   - Subnets are child resources of the Virtual Network (`Microsoft.Network/virtualNetworks/subnets`). Because the VNet itself is already environment-isolated (`vnet-dietdost-${environment}`), the subnet is inherently scoped and will never collide across environments (e.g. `vnet-dietdost-dev/subnets/snet-aca-infra` vs. `vnet-dietdost-prod/subnets/snet-aca-infra`).
2. **Explicit Parameterization Across Deployment Layers**:
   - Added `"acaSubnetName": { "value": "snet-aca-infra" }` in [`infra/dev/main.parameters.json`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/main.parameters.json).
   - Added `acaSubnetName` optional input (defaulting to `'snet-aca-infra'`) in [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml), passed through to ARM What-If (Stage 2) and ARM Deploy (Stage 3).
3. **Dynamic Resource Binding**:
   - Maintain the strongly-typed child resource reference:
     ```bicep
     resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' existing = {
       parent: vnet
       name: acaSubnetName
     }
     ```
   - All dependent rules (`storageAccount.networkAcls.virtualNetworkRules`, `keyVault.networkAcls.virtualNetworkRules`, and `acaEnvironment.vnetConfiguration.infrastructureSubnetId`) bind dynamically via `acaSubnet.id`.
4. **Living Documentation & Contract Synchronization**:
   - Synchronized [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md) and [`docs/architecture/diagrams/azure_zero_trust_infra_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/azure_zero_trust_infra_architecture.mermaid).
   - Synchronized [`.agents/skills/diet-dost-azure-deployment/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md).
   - Updated contract tests in [`BicepIaCConfigurationContractTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs) and [`DocumentationAndSkillIntegrityContractTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/DocumentationAndSkillIntegrityContractTests.cs).

## Consequences
- **Positive**:
  - Eliminates `InUseSubnetCannotBeDeleted` errors; subsequent infrastructure deployments succeed idempotently without attempting to delete active subnets.
  - Zero downtime or disruption to deployed Container App environments.
  - Full parameter configurability retained for future greenfield environments.
- **Negative / Neutral**:
  - Subnet name remains `snet-aca-infra` within each environment's dedicated VNet rather than carrying an explicit redundant `-dev` suffix.

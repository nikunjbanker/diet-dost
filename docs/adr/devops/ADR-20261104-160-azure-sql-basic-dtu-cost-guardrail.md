<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261104-160: Azure SQL Basic DTU Cost Guardrail

> **Date / Timestamp**: 2026-11-04T00:43:23+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Diet-Dost Engineering  
> **Change Type**: DEFECT_FIX  
> **Affected Subsystems**: Infrastructure, CI/CD, Database  
> **Associated PR & Stack**: Not yet created  
> **Relevant SDDs & CFTs**: [`docs/sdd/05_devops_and_infrastructure.md`](../../sdd/05_devops_and_infrastructure.md), [`docs/sql-setup-guide.md`](../../sql-setup-guide.md)

---

## 1. Executive Summary & Change Rationale

- Pin Azure SQL provisioning to the Basic DTU service objective instead of General Purpose Serverless.
- Remove serverless-only SKU and free-quota configuration so deployment cannot silently introduce a vCore-priced database.
- Apply Basic DTU to existing databases on their next Bicep deployment, as confirmed by the user.

## 2. Context and Problem Statement

`infra/dev/sql.bicep` provisioned SKU `GP_S_Gen5_1` with General Purpose Serverless settings. The infrastructure and application deployment workflows described that serverless/free offer, creating a cost and configuration mismatch with the required Azure SQL DTU Basic tier.

## 3. Decision Drivers

- Enforce the requested DTU-based Basic SKU through the deployment source of truth.
- Make database capacity and Basic-tier storage limits explicit.
- Preserve existing SQL Server, networking, credentials, Key Vault, and application provider integration.
- Prevent future workflow descriptions and tests from drifting back to serverless.

## 4. Considered Options

- **Option 1**: Keep General Purpose Serverless and adjust free-limit settings. Rejected because the required deployment tier is DTU Basic and serverless capacity can incur vCore charges.
- **Option 2**: Provision Azure SQL Basic DTU and remove serverless-only settings. Chosen.
- **Option 3**: Leave existing databases on serverless and only change future provisioning. Rejected after the user confirmed the Basic tier should apply to existing databases on their next deployment.

## 5. Decision Outcome

- **Chosen Option**: Azure SQL Basic DTU (`Basic` service objective, 5 DTUs, maximum 2 GB).
- **Justification**: Bicep pins the desired service objective and removes General Purpose Serverless and free-limit/auto-pause properties. Both deployment workflows expose the Basic tier in their summaries, and contract tests enforce the invariant.

## 6. Consequences & Trade-Offs

### Positive Consequences

- Deployment no longer selects a General Purpose Serverless vCore SKU.
- The database capacity and storage ceiling are visible in infrastructure and deployment summaries.

### Negative Consequences / Accepted Trade-Offs

- Basic is a paid provisioned tier; its regional price applies.
- Basic is limited to 5 DTUs and 2 GB. Existing data must fit within that maximum before a deployment applies the SKU. Operators should back up the database before deploying.

## 7. Verification & Compliance Results

- **Automated Builds & Tests**: Pending implementation validation.
- **Azure SQL limits**: Microsoft Learn documents Basic as 5 DTUs with a 2 GB maximum: [DTU resource limits for single databases](https://learn.microsoft.com/azure/azure-sql/database/resource-limits-dtu-single-databases?view=azuresql#single-database-storage-sizes-and-compute-sizes).
- **Living Documentation Synchronization**: Pending `pwsh -File scripts/sync-adr-index.ps1`.

## 8. Forward Roadmap Impact & Future Phase Compatibility

The change is isolated to the Azure SQL database SKU and deployment documentation. EF Core provider selection, SQL schema reconciliation, network controls, and application persistence contracts remain unchanged. Any future tier increase requires an explicit reviewed Bicep change and must include cost and capacity validation.

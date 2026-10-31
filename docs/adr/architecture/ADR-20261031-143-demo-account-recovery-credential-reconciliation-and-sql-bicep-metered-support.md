# ADR-143: Demo Account Recovery Credential Reconciliation and Azure SQL Metered Bicep Support

## Status
Accepted

## Date
2026-10-31

## Context
Following the deployment of permanent Zero-PII recovery kit preservation (ADR-141), end-to-end verification on the live production environment (`https://dev.diet-dost.in`) revealed two operational findings:
1. **Demo Account Seed Divergence**: The seeded demo user `usr_87c369c1e3f8` ('Nikunj QR') had been previously seeded in Azure SQL before ADR-141, leaving its database row with historical, non-deterministic recovery credentials. Because `DatabaseInitializationExtensions.cs` only performed insert on `existingAnon == null`, the pre-existing user row was not reconciled with the newly derived permanent mnemonic (`suspect vintage animal morning require leader genre youth very home trust jump`).
2. **Azure SQL Metered Billing Bicep Incompatibility**: After transitioning Azure SQL Serverless Free Tier database to `BillOverUsage` (`useFreeLimit: false`) following free quota exhaustion (Error 42119), any subsequent ARM deployment running `infra/dev/sql.bicep` with hardcoded `useFreeLimit: true` failed with Azure ARM error: `ProvisioningDisabled: Cannot update paid database to free database.`

## Decision
1. **Dynamic Demo Credential Reconciliation**:
   In `DatabaseInitializationExtensions.cs`, implemented an explicit reconciliation branch for `existingAnon != null`:
   ```csharp
   if (existingAnon.RecoveryLookupId != derivedLookupId ||
       existingAnon.RecoveryKeyHash != derivedHashHex ||
       existingAnon.RecoverySalt != derivedSaltHex)
   {
       existingAnon.RecoveryLookupId = derivedLookupId;
       existingAnon.RecoveryKeyHash = derivedHashHex;
       existingAnon.RecoverySalt = derivedSaltHex;
       existingAnon.RecoveryKeyCreatedAtUtc = DateTime.UtcNow;
       await db.SaveChangesAsync();
   }
   ```
   This ensures that upon application startup, any existing demo account row in SQLite or Azure SQL is safely and idempotently reconciled to match the published demo mnemonic credentials.
2. **Azure SQL Bicep Parameterization**:
   Updated `infra/dev/sql.bicep` to introduce configurable parameters `useFreeLimit bool = false` and `freeLimitExhaustionBehavior string = 'BillOverUsage'`. This prevents ARM deployment failures against active, metered Azure SQL databases while maintaining flexibility for new environments.
3. **Frontend Recovery Client Wiring**:
   Updated `src/Nutrition.WebGateway/wwwroot/js/services/auth-service.js` to explicitly pass `rotateKey = false` to `/api/auth/recover`.

## Consequences
- The seeded demo account `usr_87c369c1e3f8` is automatically reconciled and accessible via its 12-word recovery mnemonic across all environments.
- Both newly registered anonymous users and seeded demo accounts can perform repeatable logins using mnemonic phrases or JSON recovery kits without credential rotation or session loss.
- Infrastructure and application deployment pipelines succeed idempotently without ARM database update conflicts.

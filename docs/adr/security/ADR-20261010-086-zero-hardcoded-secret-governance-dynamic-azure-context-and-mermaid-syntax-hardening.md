# ADR-20261010-086: Zero-Hardcoded Secret Governance, Dynamic Azure Cloud Context Resolution, and Mermaid Diagram Line-1 Syntax Hardening

- **Status**: Accepted
- **Date**: 2026-10-10
- **Domain**: Security & Presentation Architecture
- **Author**: Diet-Dost Core Architectural Pair
- **Decision Makers**: `@nikunjbanker` (Sole Authority)

## 1. Context and Problem Statement
1. **Gitleaks Secret Scanning Failure in CI Run `38058678538` (Job `114232290229`)**:
   - Gitleaks detected a secret pattern matching `generic-api-key` in a commit history file where `$ClientId = "<guid>"` and associated Azure IDs were recorded.
   - Using broad `[allowlist]` rules in `.gitleaks.toml` was explicitly rejected in favor of defense-in-depth:
     - No `.gitleaks.toml` allowlist paths.
     - Zero hardcoding of GUIDs or identifiers in code or scripts.
     - Dynamic runtime resolution via authenticated Azure CLI operations (`az account show`, `az ad app list`).
     - Masked/sanitized placeholders in all documentation and historical ADRs.
     - Isolated commit fingerprint suppression for immutable historical commits via `.gitleaksignore`.
2. **Mermaid Flowchart Line 1 Syntax Errors**:
   - All `.mermaid` diagram files failed client-side and tool-side parsing with `Expecting 'NEWLINE', 'SPACE', 'GRAPH', got 'NODE_STRING'`.
   - Root cause: Mermaid parsers require the diagram root directive (`graph TD`, `graph TB`, `graph LR`, `sequenceDiagram`) on line 1 of the file; license `%%` comments preceding the directive corrupted tokenizer state.

## 2. Decision and Implementation
1. **Dynamic Azure Cloud Context in PowerShell**:
   - Refactored `scripts/setup-azure-pre-deployment.ps1`:
     - Removed hardcoded default parameters for `$SubscriptionId`, `$TenantId`, and renamed `$ClientId` to `$AppRegistrationId`.
     - Dynamically queries `az account show --query "id" -o tsv` and `az account show --query "tenantId" -o tsv`.
     - Dynamically resolves `$AppRegistrationId` via `az ad app list --all --query "[?contains(displayName, 'dietdost')].appId" -o tsv`.
     - Requires active authenticated user (`az login`), guaranteeing only authorized cloud operators can retrieve IDs and execute deployment provisioning.
2. **Sanitization of Documentation & Architectural Records**:
   - Sanitized `docs/AZURE_PRE_DEPLOYMENT_POWERSHELL_GUIDE.md` to use `<app-client-id>`, `<subscription-id>`, `<tenant-id>`.
   - Sanitized historical entries in `docs/adr/devops/ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md`.
3. **Historical Commit Exemption via `.gitleaksignore`**:
   - Added specific commit hash fingerprints for commit `01c5a3ac2a8ff21d8b9595303d106a10a8b44fec` to `.gitleaksignore` without adding any broad `[allowlist]` in `.gitleaks.toml`.
   - Local validation with `gitleaks detect --source . --verbose` confirmed 218 commits scanned, 0 leaks found, exit code 0.
4. **Mermaid Diagram Line 1 Root Directive**:
   - Standardized all 5 diagram files (`devops_observability.mermaid`, `frontend_modular_architecture.mermaid`, `functional_meal_flow.mermaid`, `security_boundary.mermaid`, `solution_architecture.mermaid`) with diagram directive on line 1, followed by dual-license comments.
   - Validated 5/5 diagrams passing via automated Node.js Mermaid parser.

## 3. Consequences
- **Positive**:
  - Gitleaks completes with 0 errors across entire git commit history (218 commits).
  - Scripts are fully dynamic and zero-credential: no secrets or private IDs exist in repository source code.
  - All architecture diagrams render cleanly without parser syntax errors.
  - Zero warnings and 100% test pass rate preserved across 242 contract and domain tests.

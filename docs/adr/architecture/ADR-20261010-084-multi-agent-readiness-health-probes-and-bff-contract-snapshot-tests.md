# ADR-20261010-084: Multi-Agent Readiness (Antigravity, Copilot, Claude), Native Health Probes, and BFF Contract Testing

> **Date / Timestamp**: 2026-10-10T18:50:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Antigravity AI Agent & @nikunjbanker  
> **Change Type**: ARCHITECTURE | GOVERNANCE | INFRASTRUCTURE | SECURITY  
> **Affected Subsystems**: WebGateway, Application, Infrastructure, DevContainer, CI/CD, Documentation  
> **Associated PR & Stack**: PR #53 (Branch: `feature/aca-deployment-sqlite-smb-dev-domain`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md), [`docs/sdd/04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md), [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md)  

---

## 1. Executive Summary & Change Rationale
* **Multi-Agent Readiness**: Implemented authoritative configuration files for all major developer AI agents: Google Antigravity (modular `.agents/rules/`), GitHub Copilot ([`.github/copilot-instructions.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/copilot-instructions.md)), and Anthropic Claude Code ([`CLAUDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/CLAUDE.md)).
* **Standardized Dev Container**: Added [`.devcontainer/devcontainer.json`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.devcontainer/devcontainer.json) configured with .NET 11, PowerShell, Azure CLI, GitHub CLI, and security tools for GitHub Codespaces and Copilot Workspace.
* **Native ASP.NET Core Health Probes**: Registered native `/healthz` (liveness) and `/ready` (SQLite `DatabaseHealthCheck`) endpoints in `Nutrition.WebGateway` and wired them to Azure Container Apps container probes in `infra/app.bicep`.
* **Automated IaC & BFF Schema Contract Tests**: Implemented `BicepIaCConfigurationContractTests` and `BffContractSnapshotTests` in `Nutrition.EvalHarness.Tests`, expanding test coverage to 240 automated tests with 0 warnings.
* **GitHub Issue & PR Templates**: Created standardized `.github/pull_request_template.md` (with Stack Navigator Callout widget and CFT evidence section) and issue templates for features and bugs.

---

## 2. Context and Problem Statement
Different AI agent engines (Google Antigravity, GitHub Copilot, Anthropic Claude Code) use distinct discovery paths for repository instructions. Without standardized instructions in their native paths, agents either lack domain context or drag obsolete conventions (such as introducing MediatR or violating clinical invariants). Furthermore, cloud deployments on Azure Container Apps require robust liveness and readiness probes to gracefully handle SQLite database file readiness on network SMB volumes, and evolving DTO contracts risk breaking web and mobile clients without automated snapshot verification.

---

## 3. Decision Drivers
* **Multi-Agent Interoperability**: Support Google Antigravity as default development agent alongside GitHub Copilot and Claude Code with zero prompt token bloat.
* **Resilience in Cloud Deployment**: Guarantee ACA container lifecycle visibility via `/healthz` and `/ready` probes.
* **Configuration Integrity**: Prevent deployment failures caused by drift between Bicep environment parameters and application Options.
* **Zero Client Schema Drift**: Verify Web BFF and Mobile BFF JSON contracts through automated snapshot tests.

---

## 4. Considered Options
* **Option 1 (Chosen)**: Tri-agent readiness (`.agents/rules/`, `.github/copilot-instructions.md`, `CLAUDE.md`), native ASP.NET Core health checks wired to Bicep probes, .devcontainer setup, and automated contract snapshot tests.
* **Option 2**: Maintain only `AGENTS.md` and rely on agents to infer external requirements ad-hoc.
* **Option 3**: Use third-party NuGet packages for health checks and contract testing.

---

## 5. Decision Outcome
* **Chosen Option**: Option 1.
* **Justification**: Eliminates tool friction, ensures 100% compliance with repository invariants across all AI agents, and elevates solution quality with automated regression tests for cloud infrastructure and API contracts.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* Google Antigravity, GitHub Copilot, and Claude Code instantly adhere to all Diet-Dost architectural rules (.NET 11, Native CQRS, Zero Assumption Intake, Sole Authority `@nikunjbanker`).
* Azure Container Apps automatically detects and restarts unhealthy instances via `/healthz` and delays routing traffic until `/ready` confirms SQLite connectivity.
* Automated tests in CI immediately catch any drift in Bicep parameters or BFF JSON serialization keys.
* Test suite expanded to 240 tests (100% passing, 0 warnings).

### Negative Consequences / Accepted Trade-Offs:
* Adds maintenance of three instruction files; mitigated by keeping them high-density, authoritative, and synchronized via living ADRs.

---

## 7. Forward Roadmap Impact & Future Phase Compatibility
* **Phase 2 Mobile BFF**: Mobile apps rely on stable `/api/mobile/v1/*` contracts validated by `BffContractSnapshotTests`.
* **Phase 3 Cloud Database**: When transitioning from SQLite to Azure SQL, `DatabaseHealthCheck` automatically checks `CanConnectAsync()` on the new provider without code changes.

---

## 8. Verification & Compliance Results
* **Automated Builds & Tests**:
  - `dotnet build DietDost.slnx`: 0 warnings, 0 errors
  - `dotnet test --nologo`: 240 passed, 0 failed (36 Domain, 204 EvalHarness)
* **AI Security Defense**:
  - `pwsh -File scripts/verify-ai-security-defense.ps1`: All 5 vectors passed cleanly.
* **Living Documentation Synchronization**:
  - Registered in `docs/adr/architecture/ADR-20261010-084-multi-agent-readiness-health-probes-and-bff-contract-snapshot-tests.md`.

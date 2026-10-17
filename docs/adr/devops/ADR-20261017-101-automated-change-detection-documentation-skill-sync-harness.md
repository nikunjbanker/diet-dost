<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-101: Automated Change-Detection Documentation & Skill Synchronization Test Harness (Vector 7 & Contract Testing)

| Metadata | Details |
| :--- | :--- |
| **Status** | **ACCEPTED** |
| **Date** | 2026-10-17 |
| **Domain** | DevOps / Quality Engineering & DevSecOps |
| **Subsystem** | `scripts/verify-ai-security-defense.ps1`, `tests/Nutrition.EvalHarness.Tests`, `AGENTS.md`, `.agents/rules/` |
| **Impacted Files** | `scripts/verify-ai-security-defense.ps1`, `tests/Nutrition.EvalHarness.Tests/DocumentationAndSkillIntegrityContractTests.cs`, `AGENTS.md`, `.agents/rules/skill-synchronization-governance.md`, `docs/sdd/05_devops_and_infrastructure.md`, `docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`, `docs/sdd/10_configuration_and_secret_management_architecture.md`, `.agents/skills/diet-dost-azure-deployment/SKILL.md`, `.agents/skills/diet-dost-devsecops-pipeline/SKILL.md`, `.agents/skills/diet-dost-user-management-security/SKILL.md` |

---

## 1. Context & Forensic Failure Analysis

During the migration and environment isolation of Azure Bicep templates from `infra/` to `infra/dev/` and `infra/<env>/`, code modifications, workflow updates, and C# contract tests were committed and passed cleanly. However, the associated Software Design Documents (`docs/sdd/05_devops_and_infrastructure.md`, `docs/sdd/10_configuration_and_secret_management_architecture.md`) and agent skills (`.agents/skills/diet-dost-azure-deployment/SKILL.md`) were **not synchronized automatically**. The user had to intervene and explicitly prompt the agent to perform the synchronization.

A forensic root cause analysis identified three major contributing factors:

### 1. Purely Declarative (Cognitive) Rule vs. Missing Programmatic Gate
`AGENTS.md` (Step 3: Major Change Auto-Detection & Living Synchronization) and `.agents/rules/skill-synchronization-governance.md` defined change-detection synchronization purely as an **English-language directive**. In autonomous agentic coding:
- The agent's working memory was fixated on syntax fixes, Bicep parameterization, and passing unit tests.
- Because `dotnet test`, `tests/validate_e2e_tiers.ps1`, and `scripts/verify-ai-security-defense.ps1` all returned exit code 0, the agent had **zero programmatic indication** that documentation was stale or links were broken.
- **The Core Axiom**: *"A rule without an automated failing test is merely an aspiration."* Without a failing exit code, agents will reliably succumb to cognitive tunnel vision.

### 2. False Sense of Security from Vector 6
`scripts/verify-ai-security-defense.ps1` previously included `Vector 6: Living Architecture Diagram Synchronization`. However, Vector 6 only ran `scripts/sync-architecture-diagrams.ps1 -Verify`, which only verified `.mermaid` syntax and alignment with `docs/sdd/02_solution_architecture.md` and `README.md`. It did **not** inspect other SDDs, CFTs, agent skills, or general markdown link resolution. It emitted green `✅ All architecture diagrams verified syntactically valid and 100% aligned with solution`, giving a misleading signal of complete documentation synchronization.

### 3. Lack of Link Integrity and Change-Detection Mechanics in Test Harnesses
When files like `infra/infra.bicep` were moved, references to the old paths in documentation became dead links. Neither the pre-commit script nor `Nutrition.EvalHarness.Tests` had a validator scanning markdown links against the filesystem or asserting that changes in `infra/` triggered required updates in `docs/sdd/05_devops_and_infrastructure.md` and `.agents/skills/diet-dost-azure-deployment/`.

---

## 2. Decision & Architecture Solution

To permanently prevent documentation and skill drift and eliminate reliance on human reminders, we implement a **two-tier programmatic enforcement harness**:

### 1. Programmatic Contract Testing (`Nutrition.EvalHarness.Tests/DocumentationAndSkillIntegrityContractTests.cs`)
Integrated directly into the .NET test suite executed on every `dotnet test`:
- **`All_Markdown_FileLinks_In_SDDs_And_Skills_MustResolveToExistingFiles`**: Recursively parses all markdown links (`[text](target)` and `file:///` URIs) across `docs/sdd/`, `docs/cft/`, `.agents/skills/`, and `.agents/rules/`. Validates that every target resolves to an existing file or directory on disk.
- **`No_Unscoped_Legacy_Bicep_Paths_Referenced_In_Living_Documentation`**: Scans living documents to detect un-scoped references to `infra/infra.bicep` or `infra/app.bicep` (mandating `infra/<env>/` or `infra/{env}/`).
- **`Infrastructure_Documentation_And_Skill_Must_Reflect_Current_Bicep_Structure`**: Asserts that `05_devops_and_infrastructure.md` and `diet-dost-azure-deployment/SKILL.md` accurately declare current infrastructure naming conventions (`snet-aca-infra-${environment}`).

### 2. Automated Vector 7 in `scripts/verify-ai-security-defense.ps1`
Added **Vector 7: Living Documentation, Skill Link Integrity & Automated Change-Detection Gate**:
- **Subsystem Change Detection Engine**: Detects modified files via `git diff` across 5 architectural boundaries:
  - `infra/**` $\to$ Requires `docs/sdd/05_devops_and_infrastructure.md`, `.agents/skills/diet-dost-azure-deployment/`, `docs/cft/`, or `docs/adr/devops/`.
  - `src/Nutrition.Presentation/**` or `wwwroot/**` $\to$ Requires `03_presentation_layer.md`, `design-system-enforcer`, `diet-dost-clean-architecture`, or `docs/adr/presentation/`.
  - `src/Nutrition.Application/**` $\to$ Requires `02_solution_architecture.md`, `diet-dost-clean-architecture`, or `docs/adr/architecture/`.
  - `src/Nutrition.Infrastructure/AI/**` $\to$ Requires `11_ai_in_sdlc_and_agentic_workflows_architecture.md`, `SECURITY.md`, `diet-dost-user-management-security`, or `docs/adr/security/`.
  - `src/Nutrition.Domain/**` $\to$ Requires `01_clinical_dietetics_and_nutrition_science.md`, `02_solution_architecture.md`, `indian-diet-calorie-tracker`, or `docs/adr/architecture/`.
- If any subsystem code is modified without staging/committing changes to its designated living SDD or skill, Vector 7 **fails with exit code 1 and outputs the exact missing documentation files**.
- Executes in CI Job 7 (`ai-security-defense`) and locally before PR creation.

### 3. Repository Rule Codification (`AGENTS.md` and `.agents/rules/`)
Updated `AGENTS.md` Step 3 and `.agents/rules/skill-synchronization-governance.md` to:
- Document the deterministic programmatic gates.
- Require passing `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All` (Vector 7) and `dotnet test` prior to PR creation.
- Define zero-tolerance policy for dead markdown links or stale path references.

---

## 3. Implementation Evidence & Validation Metrics

1. **Dead Link Remediation**:
   - Fixed broken ADR links in `docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md` (repointed ADR-049, ADR-051, ADR-052, ADR-060 to domain folders).
   - Fixed broken relative paths in `.agents/skills/diet-dost-azure-deployment/SKILL.md`, `.agents/skills/diet-dost-devsecops-pipeline/SKILL.md`, and `.agents/skills/diet-dost-user-management-security/SKILL.md`.
   - Updated Bicep references in `docs/sdd/10_configuration_and_secret_management_architecture.md`.
2. **Contract Test Execution**:
   - `DocumentationAndSkillIntegrityContractTests` executed: 3 tests, 3 passed, 0 failed in 409 ms.
3. **AI Defense Validator Execution**:
   - `scripts/verify-ai-security-defense.ps1 -Mode All` passed all 7 vectors cleanly.
   - Vector 7 verified 0 dead links, 0 stale Bicep paths, and 100% subsystem change-detection alignment.

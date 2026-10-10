<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260928-048: Autonomous Issue-Driven Development (IDD) Skill & Resumable Local State Engine

> **Date / Timestamp**: 2026-09-28T13:02:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[ARCHITECTURE]`, `[GOVERNANCE]`, `[SKILL]`, `[WORKFLOW]`  
> **Affected Subsystems**: Agent Skills (`.agents/skills/diet-dost-issue-driven-workflow/`), State Engine (`.agents/state/`), Governance (`AGENTS.md`)  
> **Associated PR & Stack**: PR #23 (Base: `docs/introduce-dedicated-adr`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md), [`docs/cft/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/)  

---

## 1. Executive Summary & Change Rationale
* Codified authoritative autonomous issue-driven development skill [`.agents/skills/diet-dost-issue-driven-workflow/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-issue-driven-workflow/SKILL.md) and runner script.
* Established the 5-phase closed loop: `DETECT -> ACT (Analyze) -> IMPLEMENT -> VALIDATE -> TEST / RETEST -> DRAFT PR`.
* Solved the credit constraint: engineered for local execution on developer workstation via GitHub CLI (`gh`), eliminating reliance on paid GitHub AI credits.
* Implemented durable state persistence in `.agents/state/issue_workflow_state.json` to guarantee interruption resilience and resume support.
* Mandated the Zero-Unilateral-Decision rule, Customer & Functional Acceptance Test (CFT) lifecycle maintenance, and atomic ADR generation.

---

## 2. Context and Problem Statement
When developing features or resolving defects from GitHub issues, autonomous agents need a structured, reliable, and fault-tolerant process.

In addition, running agentic workflows inside GitHub-hosted infrastructure requires paid agent credits that may not be available. Therefore, the workflow must execute locally on the developer's workstation while remaining resilient to network disconnects, system reboots, and agent restarts.

Finally, unguided autonomous execution risks architectural divergence, breaking clinical invariants, or opening unapproved PRs without user consensus. A formal state machine with mandatory user confirmation guards was required.

---

## 3. Decision Drivers
* **Local Execution Guarantee**: Run entirely on the local developer workstation using `gh` CLI without requiring GitHub cloud agent credits.
* **Resumability & Interruption Safety**: Checkpoint progress after every phase in durable JSON memory (`.agents/state/issue_workflow_state.json`), enabling instant resumption upon system reboot or reconnect.
* **Zero-Unilateral-Decision Mandate**: Enforce interactive user confirmation (`ask_question`) whenever ambiguity, architectural choices, or missing specifications arise.
* **Living Documentation & Parity**: Automatically generate atomic ADR fragments, synchronize SDD blueprints, and update/create executable CFT acceptance checklists in `docs/cft/`.
* **Zero-Warning Standard**: Enforce 0 warnings on `.NET 11` builds, dual license compliance, and 5-tier user validation.

---

## 4. Considered Options
* **Option 1 (Cloud-Only GitHub Action / Agent)**: Rely on GitHub-hosted agents.
  - *Cons*: Blocked by user's lack of cloud agent credits; recurring costs.
* **Option 2 (Ad-Hoc Manual Prompting)**: Prompt AI manually for each issue without state tracking.
  - *Cons*: Error-prone, no interruption recovery, high token overhead, inconsistent test/ADR synchronization.
* **Option 3 (Autonomous Local IDD Skill with Resumable State)**:
  - Implement `.agents/skills/diet-dost-issue-driven-workflow/`.
  - Provide local runner script `scripts/issue_workflow_runner.ps1`.
  - Maintain durable local state in `.agents/state/issue_workflow_state.json`.
  - Execute Detect -> Act -> Implement -> Validate -> Test / Retest -> Draft PR.

---

## 5. Decision Outcome
* **Chosen Option**: **Option 3 (Autonomous Local IDD Skill with Resumable State)**
* **Justification**:
  - Eliminates cloud credit bottlenecks completely.
  - Guarantees deterministic state recovery across reboots.
  - Prevents unilateral decisions through strict `ask_question` modal gates.
  - Keeps living documentation and CFT acceptance tests 100% synchronized.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Zero Cloud Cost**: Entire issue lifecycle executes locally on the developer's hardware.
* **Durable Fault Tolerance**: Machine shutdown or network drops do not lose progress; execution resumes from the exact saved checkpoint.
* **Consistent Quality**: Every issue resolution automatically passes static analysis, 5-tier verification, unit tests, CFT execution, and dual AGPLv3/SSPL v1 validation.
* **Continuous Documentation**: ADRs and CFTs are created/updated in the exact same PR as the implementation.

### Negative Consequences / Accepted Trade-Offs:
* Requires the developer's workstation to be running and authenticated via `gh auth status` for automated polling. (Handled gracefully with `ON_HOLD` state storage when offline).

---

## 7. Verification & Compliance Results
* **Automated Builds & Tests**:
  - `dotnet build --configuration Release`: 0 warnings, 0 errors
  - `dotnet test --configuration Release`: 129 passed, 0 failed, 0 warnings
* **License Header Verification**:
  - `verify_license_headers.ps1`: All eligible source and markdown files compliant under dual AGPLv3 / SSPL v1 licensing.
* **Local State Engine**: Tested initialization and state persistence in `.agents/state/issue_workflow_state.json`.
* **Living Documentation Synchronization**:
  - Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`

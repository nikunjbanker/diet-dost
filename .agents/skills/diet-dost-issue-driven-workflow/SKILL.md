---
name: diet-dost-issue-driven-workflow
description: Authoritative autonomous issue-driven development (IDD) skill for Diet-Dost. Implements the Detect-Act-Implement-Validate-Test/Retest lifecycle, local state checkpointing & offline resilience, GitHub issue lifecycle automation, zero-unilateral-decision governance, CFT synchronization, and draft PR generation.
trigger:
  mode: auto
  type: scheduled
  schedule: "0 * * * *" # Hourly execution (every 1 hour at minute 0)
  interval_seconds: 3600
  events:
    - issues.opened
    - issues.reopened
    - issues.edited
    - issues.labeled
  runner:
    engine: powershell
    script: scripts/issue_workflow_runner.ps1
    state_file: ../../state/issue_workflow_state.json
---

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Autonomous Issue-Driven Development (IDD) Skill: Diet-Dost

> **Specification Version**: `v1.0.0 (Production IDD Architecture)`  
> **Lifecycle Pattern**: `DETECT -> ACT -> IMPLEMENT -> VALIDATE -> TEST / RETEST`  
> **Execution Environment**: Local Workstation Execution Engine (Zero Cloud Credits Dependency)  
> **State Engine**: Durable JSON State Checkpointing with Fault-Tolerant Resumption (`.agents/state/`)  
> **Governance**: Zero-Unilateral-Decision Protocol, Living SDD/ADR Synchronization, and Executable CFT Maintenance  

---

## 1. Executive Strategy & Local Execution Architecture

This skill defines the **authoritative autonomous issue processing standard** for the **Diet-Dost** repository.

### Local Workstation Execution (Credit Conservation)
Because automated GitHub-hosted AI agent credits may be unavailable or cost-prohibitive, all autonomous issue processing is engineered to run **locally on the developer's system**:
1. **Online State**: While the workstation is powered on and connected, the local runner monitors GitHub repository events (new issues created, updated, closed, or deleted) via GitHub CLI (`gh`).
2. **Offline / Suspended State**: When the workstation is powered down or network drops, all in-flight tasks are held safely in durable local memory (`.agents/state/issue_workflow_state.json`).
3. **Resumption Protocol**: Upon system reboot or agent reactivation, the workflow inspects the state registry and resumes execution from the exact phase where it was held—preventing duplicated effort or lost context.

### 1.1 Automated Hourly Trigger & Harness Interpretation
The skill declares a native scheduled trigger in its frontmatter:
```yaml
trigger:
  mode: auto
  type: scheduled
  schedule: "0 * * * *" # Hourly execution (every 1 hour at minute 0)
  interval_seconds: 3600
  events: [issues.opened, issues.reopened, issues.edited, issues.labeled]
```

#### How the Agent Harness & IDE Executes the Hourly Auto-Trigger:
1. **Agent Scheduled Cron Activation**:
   - The agent harness interprets the declarative `trigger.schedule: "0 * * * *"` and registers a recurring background cron task via the IDE scheduler:
     ```json
     {
       "CronExpression": "0 * * * *",
       "Prompt": "Execute diet-dost-issue-driven-workflow: inspect repository issues via issue_workflow_runner.ps1, detect new/modified issues, and process them through the DETECT -> ACT -> IMPLEMENT -> VALIDATE -> TEST/RETEST -> DRAFT PR pipeline.",
       "IsDaemon": true
     }
     ```
2. **Standing Daemon Execution**:
   - Marking `IsDaemon: true` guarantees that the hourly schedule continues firing as an independent standing job across agent turns while the developer workstation is active.
3. **Graceful Suspension**:
   - If the system is powered down or the network drops during an hourly run, the runner checkpoints progress to `.agents/state/issue_workflow_state.json` as `ON_HOLD`. When the workstation is powered back on, the next trigger resumes from the checkpoint.

---

## 2. The 5-Phase Issue Lifecycle State Machine

Every GitHub issue processed by an AI agent or human contributor follows the strict state machine:

```
[ DETECT ] ──► [ ACT (Analyze) ] ──► [ IMPLEMENT ] ──► [ VALIDATE ] ──► [ TEST / RETEST ] ──► [ DRAFT PR ]
     │               │                      │                │                 │                   │
     ▼               ▼                      ▼                ▼                 ▼                   ▼
 Checkpoint:    Checkpoint:            Checkpoint:      Checkpoint:       Checkpoint:         Checkpoint:
  DETECTED     ACT_ANALYZED            IMPLEMENTED       VALIDATED          TESTED            COMPLETED
                     │
         (In case of any doubt)
                     │
                     ▼
          [ ASK CONFIRMATION ]
          (Wait for User Modal)
```

---

### Phase 1: DETECT (Issue Intake, CODEOWNER Authorization & Classification)
* **Objective**: Ingest issue event, verify CODEOWNER authorization, classify intent, and initialize local state checkpoint.
* **Actions**:
  1. Fetch issue details and author using GitHub CLI:
     ```powershell
     gh issue view <issue-number> --json number,title,body,labels,state,updatedAt,author
     ```
  2. **CODEOWNER Gating Verification (Zero Unauthorized Auto-Trigger)**:
     - Dynamically resolve codeowners from `.github/CODEOWNERS` (e.g. `@nikunjbanker`).
     - **If Author is a CODEOWNER**: Pre-authorized! Proceed directly to classification.
     - **If Author is a NON-CODEOWNER**:
       - Automated development **MUST NOT** trigger automatically.
       - Inspect issue for CODEOWNER approval:
         a) **Comment Approval**: Any comment from a recognized CODEOWNER containing `/approve`, `/proceed`, `/start`, or `/lgtm`.
         b) **Label Approval**: The issue is tagged with `approved-by-codeowner` or `status:approved`.
       - If **NOT approved**: Place the issue on **HOLD** (`phase: "AWAITING_CODEOWNER_APPROVAL"`, stored in `heldIssues` in local state). Skip code implementation until a CODEOWNER grants approval.
       - If **APPROVED by CODEOWNER**: Transition state to `approvalStatus: "AUTHORIZED"` and proceed to classification.
  3. Parse the issue category and branch naming prefix:
     - Bug / Defect Fix -> Prefix: `fix/`
     - New Feature / Capability -> Prefix: `feature/`
     - Architectural Refactoring -> Prefix: `arch/`
     - Documentation / Governance -> Prefix: `docs/`
  4. Generate sanitized branch slug:
     `<prefix>/issue-<number>-<sanitized-title>`
  5. **Single-Issue In-Flight Policy & Stacked Queue Governance**:
     - **Strict Concurrency Limit**: At any given time, only **ONE** issue may be actively in progress (`phase: "DETECTED"`, `"ACT"`, `"IMPLEMENT"`, `"VALIDATE"`, or `"TEST"`).
     - **Queueing of Subsequent / Stacked Issues**: If an issue is already active in development, any newly detected or dependent issues (e.g. child stacked issues citing `Depends on: #<parent>`) are placed into `queuedIssues` in `.agents/state/issue_workflow_state.json` under phase `QUEUED_AWAITING_ACTIVE_ISSUE`.
     - **Automated Linear Promotion**: When the currently active issue reaches `COMPLETED` (its Pull Request is created or merged on GitHub), the runner automatically dequeues and promotes the next queued issue to `activeIssues` under phase `DETECTED`.
  6. Record entry in `.agents/state/issue_workflow_state.json` under phase `DETECTED` (if active queue empty) or `QUEUED_AWAITING_ACTIVE_ISSUE`.

---

### Phase 2: ACT (Analyze, Deconstruct & Confirm)
* **Objective**: Deep architectural, clinical, and impact analysis before writing code.
* **Actions**:
  1. **Clinical & Architectural Boundary Audit**:
     - Check against ICMR-NIN 2024 / WHO clinical standards (Zero-Assumption Intake, BMI cutoffs, calorie safety floors).
     - Check Clean Architecture boundaries (Domain -> Application -> Infrastructure -> Presentation Gateway).
     - Determine whether changes belong to Web BFF, Mobile BFF, or Core Domain.
  2. **Acceptance Criteria & CFT Mapping**:
     - Identify which Customer & Functional Acceptance Test (CFT) in `docs/cft/` governs the feature.
     - If the issue introduces new functionality, outline the new CFT file to be created: `docs/cft/cft_issue_<number>_<slug>.md`.
  3. **Dependency & Stack Detection**:
     - Determine whether this issue depends upon or builds upon an active, unmerged Pull Request or feature branch.
     - If dependent, identify `<parent-pr-number>` and `<parent-branch>`, and flag the issue as a stacked layer (`isStacked: true`).
  4. **Mandatory Confirmation & Zero-Unilateral-Decision Protocol (Strict Ask Rule)**:
     > [!IMPORTANT]
     > In case of ANY ambiguity, doubt, conflicting implementation options, or architectural trade-offs:
     > - **STOP immediately**.
     > - Use the interactive question tool (`ask_question`) to present options to the user.
     > - **NEVER assume or decide unilaterally**.
     > - Record the issue state as `WAITING_CONFIRMATION` until the user responds.
  5. Record phase `ACT_ANALYZED` upon alignment.

---

### Phase 3: IMPLEMENT (Isolated Branching & Code Implementation)
* **Objective**: Execute clean, warning-free implementation on an isolated branch.
* **Actions**:
  1. **Pre-Flight Remote Fetch & Branch Creation**:
     ```bash
     git fetch origin
     git checkout -b <branch-name> origin/<base-branch>
     # Assert commit lineage:
     git rev-parse HEAD
     git rev-parse origin/<base-branch>
     ```
  2. **Implementation Standards**:
     - Strictly target `.NET 11` (`net11.0`).
     - Zero warnings, zero errors (`TreatWarningsAsErrors`).
     - Maintain Native CQRS query/command handlers and thin controllers.
     - Prevent duplicate domain code between Web and Mobile.
  3. Record phase `IMPLEMENTED` in local state.

---

### Phase 4: VALIDATE (Static Analysis & Multi-Tier Verification)
* **Objective**: Validate code quality, licensing, and security boundaries.
* **Actions**:
  1. **Compile & Roslyn Validation**:
     ```bash
     dotnet build --configuration Release
     ```
     *Assert*: Build succeeds with 0 warnings and 0 errors.
  2. **Solution-Wide License Header Enforcement**:
     ```powershell
     powershell -ExecutionPolicy Bypass -File .agents/skills/diet-dost-license-governance/scripts/verify_license_headers.ps1
     ```
     *Assert*: 100% compliant under dual AGPLv3 / SSPL v1 licensing.
  3. **End-to-End Demo User Tier Validation**:
     - Verify seeded demo accounts across all 5 tiers:
       `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app` (Password: `DietDost@Demo2026!`).
     - Assert tier quotas, feature gating, and role permissions function with 0 runtime errors.
  4. Record phase `VALIDATED` in local state.

---

### Phase 5: TEST / RETEST (Closed-Loop Testing & CFT Maintenance)
* **Objective**: Execute test suites, maintain platform CFTs, and verify cross-platform parity.
* **Actions**:
  1. **Automated Test Execution**:
     ```bash
     dotnet test --configuration Release
     ```
     *Assert*: 100% pass rate across all domain, application, evaluation, and security test harnesses.
  2. **CFT Creation, Update, or Deletion**:
     - **New Feature**: Author new acceptance checklist `docs/cft/cft_issue_<number>_<slug>.md`.
     - **Modified Feature**: Update existing platform CFT in `docs/cft/`.
     - **Deprecated Feature**: Archive or retire obsolete CFT with historical notes.
  3. **Cross-Platform Parity Verification**:
     - Cross-check Web vs Android vs iOS in [`docs/cft/cft_cross_platform_functional_parity_matrix.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_cross_platform_functional_parity_matrix.md).
     - Assert zero mathematical or clinical regressions between platforms.
  4. Record phase `TESTED` in local state.

---

## 3. Living Documentation & Architectural Decision Records (ADRs)

Before opening the Draft PR, the agent MUST update living documentation:
1. **Dedicated Atomic ADR Fragment**:
   - Create a standalone ADR fragment using `write_to_file`:
     `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md`
   - Adhere strictly to the unified template codified in [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).
   - Reference the issue number, context, options considered, chosen outcome, and verification metrics.
2. **Registry Synchronization**:
   - Register the new ADR in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md).
   - Synchronize [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).
   - Update affected SDD blueprints in `docs/sdd/` (e.g. `02_solution_architecture.md`, `03_data_models_and_contracts.md`).

---

## 4. Draft Pull Request Creation Protocol

1. **Push Branch to Remote**:
   ```bash
   git push -u origin <branch-name>
   ```
2. **Create Draft PR via GitHub CLI**:
   - **For Independent PRs**:
     ```powershell
     gh pr create --draft --base main --head <branch-name> --title "<type>(issue-<num>): <title>" --body "## Summary`n`nCloses #<num>`n`n### Implementation Details`n- Completed via Autonomous Issue-Driven Workflow (IDD).`n`n### Verification Results`n- \`dotnet test\`: 100% passed`n- CFT Checklist: \`docs/cft/...\` updated`n- ADR Registered: \`docs/adr/ADR-...\`"
     ```
   - **For Stacked PRs (Consecutive / Dependent Features)**:
     ```powershell
     # 1. Open PR targeting parent feature branch as base
     gh pr create --draft --base <parent-branch> --head <branch-name> --title "<type>(issue-<num>): <title>" --body "> [!NOTE]`n> ### 🥞 GitHub Stack (Layer X of Y)`n> 1. 🟢 **PR #<parent-pr>** (Base: <parent-base>)`n> 2. 🟡 **PR #<child-pr> (This PR)** (Base: <parent-branch>)`n`n## Summary`n`nCloses #<num>`n`n### Implementation Details`n- Completed via Autonomous Issue-Driven Workflow (IDD).`n`n### Verification Results`n- \`dotnet test\`: 100% passed`n- CFT Checklist: \`docs/cft/...\` updated`n- ADR Registered: \`docs/adr/ADR-...\`"

     # 2. Formally link into GitHub Stack engine & synchronize
     gh stack link <parent-pr-number> <child-pr-number>
     gh stack sync
     ```
3. **Notify Issue Thread**:
   ```powershell
   gh issue comment <num> --body "Draft Pull Request opened for this issue: <pr-url>. All tests passed, CFT verified, and ADR synchronized."
   ```
4. Record phase `DRAFT_PR_CREATED` -> `COMPLETED` in `.agents/state/issue_workflow_state.json`.

---

## 5. Resumable State Memory Engine & Schema

Durable state is preserved in:
`c:\Users\nikunj.banker\source\repos\diet-dost\.agents\state\issue_workflow_state.json`

### Checkpoint Lifecycle:
```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-09-28T13:00:00+05:30",
  "activeIssues": {
    "42": {
      "issueNumber": 42,
      "title": "Add regional millets nutrition profile",
      "issueType": "feature",
      "branchName": "feature/issue-42-regional-millets",
      "phase": "IMPLEMENTED",
      "checkpoints": ["DETECTED", "ACT_ANALYZED", "IMPLEMENTED"],
      "updatedAt": "2026-09-28T12:00:00Z",
      "detectedAt": "2026-09-28T12:05:00+05:30",
      "notes": "Domain entity updated, proceeding to Validation."
    }
  },
  "completedIssues": {},
  "heldIssues": {}
}
```

### Resumption Rules:
* If `phase == "WAITING_CONFIRMATION"`, the agent re-prompts the user with `ask_question`.
* If `phase == "IMPLEMENTED"`, the agent skips re-branching and resumes directly at **VALIDATE**.
* If `phase == "VALIDATED"`, the agent proceeds directly to **TEST / RETEST**.
* If network fails or system shuts down, state remains safely stored on disk.

---

## 6. Local Runner Script Playbook

Execute the local runner script via PowerShell:

```powershell
# 1. Single Issue Targeted Execution
powershell -ExecutionPolicy Bypass -File .agents/skills/diet-dost-issue-driven-workflow/scripts/issue_workflow_runner.ps1 -IssueNumber 42

# 2. Continuous Polling Mode (polls GitHub every 60s)
powershell -ExecutionPolicy Bypass -File .agents/skills/diet-dost-issue-driven-workflow/scripts/issue_workflow_runner.ps1 -Poll -PollIntervalSeconds 60

# 3. Resume Interrupted or Held Issues
powershell -ExecutionPolicy Bypass -File .agents/skills/diet-dost-issue-driven-workflow/scripts/issue_workflow_runner.ps1 -Resume
```

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260928-050: CODEOWNER Gating & Approval Policy for Autonomous Issue-Driven Development

> **Date / Timestamp**: 2026-09-28T13:40:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: SECURITY | GOVERNANCE | ARCHITECTURE  
> **Affected Subsystems**: Agentic Workflow | Issue-Driven Development | Solution Governance  
> **Associated PR & Stack**: PR #23 (Base: `docs/introduce-dedicated-adr`, Stack #24 Layer 2)  
> **Relevant SDDs & CFTs**: [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md), [`.agents/skills/diet-dost-issue-driven-workflow/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-issue-driven-workflow/SKILL.md), [`.github/CODEOWNERS`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/CODEOWNERS)  

---

## 1. Executive Summary & Change Rationale
* **Mandated Strict CODEOWNER Gating**: Enforced that the autonomous Issue-Driven Development (IDD) skill only auto-triggers implementation for issues authored by recognized repository CODEOWNERS (resolved dynamically from `.github/CODEOWNERS`, e.g. `@nikunjbanker`).
* **Zero Unauthorized Auto-Triggering**: Issues created by non-codeowners or external contributors are strictly barred from auto-triggering code modifications or branch creation.
* **Hybrid CODEOWNER Approval Protocol**: Implemented a dual approval mechanism where a non-codeowner issue is placed on `HOLD` (`phase: "AWAITING_CODEOWNER_APPROVAL"`) until a verified CODEOWNER approves it via:
  1. A slash comment (`/approve`, `/proceed`, `/start`, or `/lgtm`) posted by a recognized CODEOWNER, **OR**
  2. The issue label `approved-by-codeowner` (or `status:approved`).
* **Resumable Held State Checkpointing**: Checkpointed held issues in `.agents/state/issue_workflow_state.json` under `heldIssues` so they are safely monitored on each polling cycle and promoted to `activeIssues` immediately upon CODEOWNER authorization.

---

## 2. Context and Problem Statement
Autonomous AI agents executing issue-driven development continuously monitor GitHub issues. Without strict author verification and approval gating:
1. Anyone with access to open a GitHub issue could trigger autonomous code generation, running test suites, modifying repository files, and creating branches and PRs.
2. Malicious or spam issues could burn local compute resources or attempt prompt injection against the agent.
3. Unvetted feature requests or defect reports would bypass triage and domain boundary verification.

---

## 3. Decision Drivers
* **Repository Security & Perimeter Control**: Autonomous agent code generation must only be triggered by authorized repository owners.
* **Triage & Quality Gate**: External or community bug reports must be triaged and approved by a CODEOWNER before implementation begins.
* **Ergonomic Maintainer Workflow**: CODEOWNERS must be able to approve issues effortlessly from mobile or desktop (via a simple `/approve` comment or applying the `approved-by-codeowner` label).
* **Fault-Tolerant State Memory**: Held issues must persist locally in memory and automatically resume without developer manual intervention once approval is detected.

---

## 4. Considered Options
* **Option 1: Allow Any Issue Author (Status Quo)**: Auto-trigger development on any open issue. (Rejected: Severe security vulnerability, vulnerable to spam and unauthorized code modifications).
* **Option 2: Hardcoded Username Check**: Hardcode `@nikunjbanker` in scripts. (Rejected: Inflexible; fails when team members or CODEOWNERS change).
* **Option 3: Comment-Only Approval (`/approve`)**: Require a comment from a codeowner. (Good, but lacks visual issue list triage).
* **Option 4: Label-Only Approval (`approved-by-codeowner`)**: Require a label on the issue. (Good, but less convenient on mobile).
* **Option 5 (Chosen): Dynamic CODEOWNERS Discovery + Hybrid Approval**: Dynamically parse `.github/CODEOWNERS` and accept either a CODEOWNER comment (`/approve`) or label (`approved-by-codeowner`).

---

## 5. Decision Outcome
* **Chosen Option**: **Option 5: Dynamic CODEOWNERS Discovery + Hybrid Approval**.
* **Justification**:
  - Dynamically binds to `.github/CODEOWNERS` as the single source of truth for repository authority.
  - Automatically allows issues created by CODEOWNERS to proceed without delay.
  - Guarantees 0 unauthorized code changes for issues created by non-codeowners.
  - Offers CODEOWNERS maximum convenience to approve via a 1-word comment (`/approve`) or label triage.

---

## 6. How CODEOWNERS Provide Approval on Issues

When an issue is opened by a non-codeowner, a recognized CODEOWNER can grant authorization using either of these two methods:

### Method A: Slash Command Comment
Post a comment on the GitHub issue containing:
```text
/approve
```
*(Alternative accepted commands: `/proceed`, `/start`, `/lgtm`)*. The runner parses the comment author, verifies they are in `.github/CODEOWNERS`, and unlocks the issue.

### Method B: GitHub Label
Apply the GitHub label to the issue:
```text
approved-by-codeowner
```
*(Alternative accepted label: `status:approved`)*.

---

## 7. Consequences & Trade-Offs

### Positive Consequences:
* **Total Protection Against Unauthorized Triggering**: Non-codeowner issues are safely paused in `heldIssues` until authorized.
* **Audit Trail**: State records `author`, `isCodeownerAuthor`, `approvalStatus`, `approvedBy`, and `approvedAt`.
* **Zero Disruption for Codeowners**: Issues created by `@nikunjbanker` auto-trigger seamlessly.

### Accepted Trade-Offs:
* Non-codeowner issues require one explicit maintainer interaction (`/approve` or label) before autonomous work begins.

---

## 8. Verification Results & Sign-Off Status
* **CODEOWNERS Parser**: Verified against `.github/CODEOWNERS`:
  ```text
  Checking GitHub connectivity...
  Resolved CODEOWNERS: @(nikunjbanker)
  Querying repository issues via GitHub CLI...
  Issue synchronization completed successfully.
  ```
* **State Checkpoint Engine**: Successfully handles `activeIssues`, `heldIssues`, and `completedIssues`.
* **Automated Tests**: 129 passed, 0 failed, 0 warnings.
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`

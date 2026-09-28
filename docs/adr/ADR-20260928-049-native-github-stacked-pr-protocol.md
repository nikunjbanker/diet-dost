<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260928-049: Native GitHub Stacked PR Protocol, gh-stack Automation & Bidirectional Navigation

> **Date / Timestamp**: 2026-09-28T13:30:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: GOVERNANCE | ARCHITECTURE | TOKEN_ECONOMICS  
> **Affected Subsystems**: Solution Governance | Agentic Workflow | Issue-Driven Development | SDD  
> **Associated PR & Stack**: PR #23 (Base: `docs/introduce-dedicated-adr`, Stack #24 Layer 2)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)  

---

## 1. Executive Summary & Change Rationale
* **Codified Native GitHub Stacked PR Management**: Standardized the use of the official `github/gh-stack` extension (`gh stack link <parent> <child>`, `gh stack sync`) for managing consecutive dependent pull requests, eliminating unlinked PR drift and resolving the GitHub banner prompt (*"This pull request can be stacked with other pull requests"*).
* **Mandated Standardized Stack Navigator Callouts**: Enforced that every PR in a stack must embed the markdown callout widget (`### 🥞 GitHub Stack #<id> (Layer X of Y)`) at the very top of its body with direct bidirectional links between parent and child PRs.
* **Integrated with Autonomous Issue-Driven Workflow**: Embedded stack dependency detection into Phase 2 (ACT) and automated stack linking into Section 4 of `.agents/skills/diet-dost-issue-driven-workflow/` and `issue_workflow_runner.ps1`.
* **Synchronized All Governing Documents**: Formally updated agent memory in `AGENTS.md` (Rules 1.d and 14), solution master skill `indian-diet-calorie-tracker/SKILL.md`, and master implementation roadmap in `docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`.

---

## 2. Context and Problem Statement
In multi-phase agentic development and phased migration roadmaps (such as the 10-PR Cross-Platform Clean Architecture & Dual BFF migration), features frequently build upon unmerged in-flight work. While branch-level stacking (`git checkout -b <child> origin/<parent>`) was previously required, GitHub introduced native support for Stacked Pull Requests.

Without formal stack registration via the GitHub Stack engine:
1. GitHub web UI displays an unformalized notification banner: *"This pull request can be stacked with other pull requests"*, creating ambiguity for human reviewers.
2. Reviewers and AI agents lack a standard visual breadcrumb navigation within the PR description to traverse up and down the dependency chain.
3. Automated tools and issue runners could accidentally open unlinked or orphaned PRs against `main`, causing massive diff pollution and merge conflicts.

---

## 3. Decision Drivers
* **Review Clarity & Zero Ambiguity**: Reviewers must see small, isolated diffs with immediate bidirectional breadcrumb links to parent and child PRs.
* **Native Platform Integration**: Utilize GitHub's native Stacked PR engine via the official `github/gh-stack` extension rather than third-party SaaS tools.
* **Autonomous Agent Reliability**: Autonomous issue-driven agents must automatically detect stack dependencies, target the correct base branch, link the stack, and inject navigation widgets.
* **Token Economics & Merge-Conflict Immunity**: Standalone PRs with atomic ADR fragments guarantee zero merge conflicts during rebases across stacked branches.

---

## 4. Considered Options
* **Option 1: Informal Base Targeting Only (Status Quo)**: Set base branch to parent branch when opening the PR on GitHub, but leave stack management unformalized. (Rejected: Triggers the unformalized banner on GitHub, lacks navigation callout, and lacks CLI sync tooling).
* **Option 2: Third-Party Stack Tooling (Graphite, Aviator, etc.)**: Adopt external commercial CLI and SaaS tools for stacked PR management. (Rejected: Requires external SaaS accounts, violates zero-cost/local-first agent principles, and introduces external vendor lock-in).
* **Option 3 (Chosen): Native GitHub Stack (`github/gh-stack`) + Mandatory Embedded Markdown Navigator Callout**: Standardize on GitHub's official native stack extension (`gh stack link`, `gh stack sync`) combined with a standardized markdown callout widget in all PR descriptions.

---

## 5. Decision Outcome
* **Chosen Option**: **Option 3: Native GitHub Stack (`github/gh-stack`) + Mandatory Embedded Markdown Navigator Callout**.
* **Justification**:
  - Leverages GitHub's native preview feature directly via the standard GitHub CLI (`gh`).
  - Formally registers the stack in GitHub's backend (`Stack #NNN`), eliminating the unresolved banner.
  - Guarantees 100% human-readable and agent-navigable PR descriptions with zero extra tooling required for reviewers.
  - Fully compatible with automated issue runners (`issue_workflow_runner.ps1`).

---

## 6. Standardized Stack Navigation Callout Template

Every stacked PR must prepend this callout at the top of its PR body:

```markdown
> [!NOTE]
> ### 🥞 GitHub Stack #<stack-id> (Layer X of Y)
> This pull request is **Layer X** of a Y-PR Stack.
>
> 1. 🟢 **PR #<parent-pr>**: `<parent-title>` (Base: `<base>`)
> 2. 🟡 **PR #<child-pr> (This PR)**: `<child-title>` (Base: `<parent-branch>`)
```

---

## 7. Consequences & Trade-Offs

### Positive Consequences:
* **Immediate Stack Lineage Visibility**: Anyone opening PR #22 or PR #23 can instantly see and navigate the complete stack.
* **Zero Branch Pollution**: Child PR diff displays only the lines introduced in that layer.
* **Automated Cascade on Merge**: When Layer 1 merges into `main`, GitHub automatically updates Layer 2's base branch to `main`.
* **Agentic Memory Persistence**: Codified in `AGENTS.md`, master skill, and issue-driven workflow skill, ensuring all future agent runs follow this exact protocol.

### Accepted Trade-Offs:
* Developers and agents must ensure `github/gh-stack` is installed (`gh extension install github/gh-stack`).
* Consecutive PRs must be merged sequentially in bottom-up order.

---

## 8. Verification Results & Sign-Off Status
* **Automated Tests**: 129 passed, 0 failed, 0 warnings across solution.
* **Stack Synchronization**: Formally registered and verified:
  ```text
  ✓ Stack on GitHub is up to date with 2 PRs (stack #24)
  ✓ Stack synced: Stacked on main
  ```
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`

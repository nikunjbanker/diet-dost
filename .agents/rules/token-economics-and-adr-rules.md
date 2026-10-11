<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Token Economics & Living ADR Architecture

## 1. System Prompt Token Conservation
- The `name` and `description` of every skill in `.agents/skills/` is injected into the AI agent system prompt on every turn.
- **Strict Anti-Pattern**: NEVER move declarative architectural specifications, threat models, or living logs into `.agents/skills/`.
- Declarative specifications belong in `docs/sdd/*.md` and consume **0 baseline tokens**. Read them on-demand via `view_file` only when relevant to the current task.

## 2. Distributed Atomic ADR Fragment Pattern
- **Eliminated Anti-Pattern**: Appending to a single monolithic log causes line-offset search errors (>46 KB) and Git merge conflicts across stacked PRs.
- **Mandatory Pattern**: Record any architectural decision in an isolated file:
  `docs/adr/<domain>/ADR-<YYYYMMDD>-<NNN>-<slug>.md`
  where `<domain>` is one of:
  - `architecture`: Topology, Clean Architecture, CQRS, DB models.
  - `security`: Auth, PromptShield, Key Vault, SAST gates.
  - `devops`: ACA, Bicep, CI/CD, SMB persistence.
  - `presentation`: PWA, Linear design tokens, UI ergonomics.
  - `governance`: Licensing, ADR hierarchy, Git branching.

## 3. Automated Indexing
- Never edit markdown tables manually.
- Run `pwsh -File scripts/sync-adr-index.ps1` to automatically regenerate:
  - `docs/adr/index.json`: Lean machine-readable manifest (~4 KB).
  - `docs/adr/README.md`: Partitioned human documentation.

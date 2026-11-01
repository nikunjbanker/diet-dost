# ADR-20261101-152: Mermaid Diagram Edge Label Quoting Invariant & GitHub Rich Display Parse Error Resolution

## Status
Accepted

## Date
2026-11-01

## Context & Problem Statement
When inspecting the repository `README.md` on GitHub, the primary architecture diagram failed to render, displaying the following parser error:

```
Unable to render rich display

Parse error on line 167:
...CA -->|Volume Mount (SMB 3.1.1)| AZURE_S
-----------------------^
Expecting 'SQE', 'DOUBLECIRCLEEND', 'PE', '-)', 'STADIUMEND', 'SUBROUTINEEND', 'PIPE', 'CYLINDEREND', 'DIAMOND_STOP', 'TAGEND', 'TRAPEND', 'INVTRAPEND', 'UNICODE_TEXT', 'TEXT', 'TAGSTART', got 'PS'
```

### Root Cause Analysis
GitHub's markdown renderer utilizes Mermaid v10+ with a Jison LALR(1) parser. In Mermaid flowchart grammar:
1. The token `PS` (`Parenthesis Start` - `(`) is a reserved lexer token that denotes the beginning of node shapes (e.g. `id(Label)`).
2. When parsing edge labels enclosed between pipes (`-->|label|`), unquoted parentheses or colons cause the lexer to break out of text tokenization and emit syntax tokens (`PS`), triggering a parse error.
3. According to Mermaid specifications and ADR-086 diagram invariants, any edge label containing special characters (`(`, `)`, `:`, `[`, `]`, `{}`) must be enclosed in double quotes:
   ```mermaid
   AZURE_ACA -->|"Volume Mount (SMB 3.1.1)"| AZURE_SMB
   ```
4. Prior to this fix, the repository's diagram linter in `scripts/sync-architecture-diagrams.py` only checked bracket balancing counts (`(` vs `)`), which matched identically and thus allowed unquoted special characters to pass undetected.

## Decision Drivers
- **GitHub UI & Documentation Integrity**: Architecture diagrams in `README.md` and SDDs must render cleanly on GitHub and all Markdown viewers.
- **Automated Regression Prevention**: Enforce edge label quoting automatically in the CI/CD and pre-commit linter (`sync-architecture-diagrams.py`) to prevent recurrence.
- **ADR-086 Governance Alignment**: Standardize edge label quoting across all 6 canonical diagram sources in `docs/architecture/diagrams/` and embedded markdown documents.
- **Zero Token Bloat**: Maintain single-source-of-truth canonical files and automated synchronization without manual drift.

## Decision Outcome
Accepted. Implemented double-quoted edge label syntax across all canonical and embedded diagrams, updated the diagram linter, and updated the solution governance rule.

### Key Implementation Details:
1. **Canonical Diagrams (`docs/architecture/diagrams/`)**:
   - `solution_architecture.mermaid`: Updated line 179 to `AZURE_ACA -->|"Volume Mount (SMB 3.1.1)"| AZURE_SMB`.
   - `devops_observability.mermaid`: Updated line 29 to `ACA -->|"Mounted Volume (SMB 3.1.1)"| SMB_Mount` and line 32 to `ACA -.->|"Application Logs Only (Entra ID)"| AppInsights_Cloud`.
   - `azure_zero_trust_infra_architecture.mermaid`: Updated lines 55, 62, 65, 66, 67, 68, 70, 71 to wrap all edge labels with colons and parentheses in double quotes.

2. **Automated Documentation Synchronization**:
   - Synchronized embedded diagrams in `README.md` and `docs/sdd/02_solution_architecture.md` via `scripts/sync-architecture-diagrams.ps1 -Sync`.
   - Quoted edge labels in `docs/sdd/05_devops_and_infrastructure.md`, `docs/sql-setup-guide.md`, and `docs/AZURE_DEVOPS_DEPLOYMENT_TODO.md`.

3. **Linter Enhancement (`scripts/sync-architecture-diagrams.py`)**:
   - Added regex-based edge label quoting validation in `validate_mermaid_syntax()`. Inspects `-->|...|`, `-.->|...|`, `==>|...|`, and `<-->|...|`. Flags any unquoted label containing `(`, `)`, `[`, `]`, `{}` or `:` as an ADR-086 violation.

4. **Governance Rule Alignment**:
   - Updated `.agents/rules/architecture-diagram-synchronization.md` (Section 2, Rule 3) to mandate quotes on both node labels and edge labels with special characters.

## Consequences & Verification
- **Positive**: GitHub renders the master solution architecture diagram with 0 errors.
- **Positive**: `pwsh -File scripts/sync-architecture-diagrams.ps1 -Verify` and `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All` (Vector 6) deterministically enforce quoted edge labels.
- **Neutral**: Requires quotes on future edge labels that include parenthetical details or colons.

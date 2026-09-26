<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Mandatory Git Branching & PR-Only Merge Mandate

## Mandatory Workflow
1. **Never commit directly to `main`**.
2. **Always create a feature, fix, or docs branch first**:
   ```bash
   git checkout -b feature/<name>
   git checkout -b fix/<name>
   git checkout -b docs/<name>
   ```
3. **Execute all changes, harness verifications, and documentation updates on the branch**.
4. **All changes must be merged into `main` using a Pull Request (PR) only**. Direct pushes or direct merges to `main` are strictly forbidden.

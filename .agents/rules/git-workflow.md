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

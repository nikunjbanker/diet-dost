/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261010-080: ESLint Code Scanning Workflow and SARIF Integration

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and Security Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
To ensure high code quality, security compliance, and early detection of client-side vulnerabilities, the repository required an automated ESLint static analysis pipeline integrated with GitHub Code Scanning.

---

## 2. Decision Drivers
- **GitHub Code Scanning Tool Integration**: Leverage GitHub Advanced Security / Code Scanning to display ESLint findings directly in the GitHub Security tab alongside CodeQL alerts.
- **SARIF Standard Compatibility**: Format analysis output using the Static Analysis Results Interchange Format (SARIF) via `@microsoft/eslint-formatter-sarif`.
- **Zero-Warning Standard**: Maintain 0 lint warnings and 0 errors across all client-side JavaScript modules.
- **Ownership & Governance**: Explicitly assign `.github/workflows/eslint.yml` to repository owner `@nikunjbanker` in `.github/CODEOWNERS`.

---

## 3. Decision Outcome
**Chosen Decision**: Configured and deployed native GitHub Actions ESLint workflow:

1. **Configuration (`.eslintrc.js`)**:
   - Configured for browser, Node, and ES2022 environments with ES modules.
   - Ignore patterns for build outputs (`**/bin/**`, `**/obj/**`, `**/dist/**`, `**/publish/**`, and `*.min.js`).
   - Protected with standard dual AGPLv3 / SSPL v1 copyright header.

2. **Workflow (`.github/workflows/eslint.yml`)**:
   - Triggers on `push` to `main`, `pull_request` targeting `main`, weekly schedule cron (`28 17 * * 5`), and manual `workflow_dispatch`.
   - Grants minimal required permissions: `contents: read`, `security-events: write`, and `actions: read`.
   - Runs `npx eslint` with `@microsoft/eslint-formatter-sarif` to produce `eslint-results.sarif`.
   - Uploads SARIF artifact to GitHub Security via `github/codeql-action/upload-sarif@v3`.

3. **Code Quality Remediations (`main.js`)**:
   - Removed unused imported symbols (`ApiClient`, `getWebDashboard`).
   - Cleaned up unreferenced local variables (`mealLogger`, `avatarEl`).
   - Verified 100% clean ESLint execution with 0 warnings and 0 errors.

4. **Security Ownership (`.github/CODEOWNERS`)**:
   - Added explicit rule locking `/.github/workflows/eslint.yml` exclusively to `@nikunjbanker`.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile & PWA Modernization**: Enforces strict JavaScript/TypeScript linting and security scanning for all future frontend and client-side modules.
- **Continuous Compliance**: Automatically alerts on regressions or new security patterns introduced in pull requests before merge into `main`.

---

## 5. Verification
- `npx eslint . --config .eslintrc.js --ext .js`: 0 problems (0 errors, 0 warnings).
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 211 / 211 tests passing.

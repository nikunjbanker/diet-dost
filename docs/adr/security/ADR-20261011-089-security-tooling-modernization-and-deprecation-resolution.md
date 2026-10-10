<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261011-089: Security Tooling Modernization, Deprecation Resolution, and Multi-Layer .NET SAST Integration

- **Status**: Accepted
- **Date**: 2026-10-11
- **Domain**: Security & DevSecOps Architecture
- **Author**: Diet-Dost Core Architectural Pair
- **Decision Makers**: `@nikunjbanker` (Sole Authority)

## 1. Context and Problem Statement
A comprehensive security tooling and scanner audit was performed across `.github/workflows/security-scan.yml`, local pre-commit hooks, and solution dependencies to identify and eliminate deprecated, End-of-Life (EOL), or unmaintained security tooling:
1. **SecurityCodeScan CLI Deprecation & Runtime Crash**:
   - The standalone `security-scan` dotnet global tool (v5.6.7) was last released in July 2021 targeting .NET Core 6.0 (EOL since November 2024).
   - On the modern .NET 11 CI runner, `security-scan` crashed silently on every project (`App launch failed: Framework Microsoft.NETCore.App 6.0.0 not found`), producing 0 SARIF results and generating an empty fallback file.
2. **ESLint 8.x End-of-Life & Deprecated Transitive Packages**:
   - `eslint@8.57.0` reached official EOL in October 2024.
   - Installation produced 6 deprecation warnings across npm packages (`glob@7.2.3` vulnerability, `inflight@1.0.6` memory leak, `rimraf@3.0.2`, `@humanwhocodes/object-schema`, `@humanwhocodes/config-array`, `eslint@8.57.0`).
3. **Outdated CLI Tool Versions**:
   - Gitleaks was pinned to v8.18.4 (upstream current: v8.30.1).
   - actionlint was pinned to v1.7.7 (upstream current: v1.7.12).
4. **Zero-Unilateral Action Constraint**:
   - Repository rules mandate that deprecated tools must not be removed unilaterally. The maintainer was consulted via interactive inquiry (`ask_question`), electing to implement **Option 1 & 2** (Microsoft Roslyn CA Security Analyzers + DevSkim CLI AND GitHub CodeQL for C#) and upgrade ESLint to flat config.

---

## 2. Decision and Implementation

### 2.1 Multi-Layered .NET 11 C# SAST Architecture (Roslyn + DevSkim + CodeQL)
To replace the abandoned 2021 `security-scan` tool with modern, actively maintained, and officially supported solutions, a defense-in-depth static analysis approach was implemented:
1. **Microsoft Roslyn Native Security Analyzers (.NET 11 SDK)**:
   - First-party Roslyn CA security rules (`CA2100` SQL injection, `CA3001`-`CA3012` OWASP taint/injection rules, `CA5350`-`CA5405` cryptography/TLS standards) are built directly into the .NET 11 compiler.
   - Executed across all 7 solution projects via `dotnet build "$proj" -c Release /p:AnalysisLevel=latest /p:AnalysisModeSecurity=All "/p:ErrorLog=$(pwd)/sarif-results/${proj_name}.sarif"`.
2. **Microsoft DevSkim CLI (`microsoft.cst.devskim.cli`)**:
   - Microsoft's open-source, actively maintained security linter CLI installed dynamically via `dotnet tool install --global microsoft.cst.devskim.cli`.
   - Scans `src/` for security-sensitive coding patterns, ignoring build artifacts, and exports standardized SARIF 2.1.0 (`sarif-results/devskim.sarif`).
3. **Consolidated SARIF Aggregation**:
   - `scripts/merge-sarif.py` aggregates all individual Roslyn and DevSkim SARIF logs into `security-code-scan.sarif`, uploaded directly to GitHub Code Scanning via `github/codeql-action/upload-sarif`.
4. **GitHub CodeQL for C#**:
   - Integrated `github/codeql-action/init` and `github/codeql-action/analyze` with manual build tracing, providing deep semantic taint analysis for C# (.NET 11).

### 2.2 ESLint 9+ Flat Configuration Migration
Migrated client-side static analysis from legacy `.eslintrc.js` to modern flat configuration:
- Created [`eslint.config.mjs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/eslint.config.mjs) with zero external dependencies, defining browser and Node.js globals natively.
- Removed deprecated `.eslintrc.js` and removed legacy `--config` and `--ext` CLI flags.
- Updated workflow step to run `npm install eslint @microsoft/eslint-formatter-sarif` and `npx eslint . --format @microsoft/eslint-formatter-sarif --output-file eslint-results.sarif`.
- Completely eliminated all 6 npm deprecation warnings.

### 2.3 CLI Tooling Upgrades
- **Gitleaks**: Upgraded from `8.18.4` to `8.30.1`, incorporating the latest entropy heuristics and secret detection rules.
- **actionlint**: Upgraded from `1.7.7` to `1.7.12`, incorporating the latest GitHub Actions syntax rules and shell injection detections.
- **Trivy**: Verified at current latest stable `0.75.0`.
- **Checkov**: Verified running cleanly on pinned commit SHA against Bicep templates.

---

## 3. Consequences and Verification

### 3.1 Positive Impacts
- **Zero Deprecation Warnings**: Completely eliminated all deprecation and EOL notices across CI scans.
- **Functional C# SAST**: Replaced a silently failing 2021 tool with real, active compiler-level and semantic analysis (Roslyn + DevSkim + CodeQL).
- **Zero Cost & Free-Tier Containment**: All adopted tools are 100% free, open-source, and natively supported on GitHub Actions runners.
- **Supply Chain Security**: Pinned GitHub Actions commits preserved.

### 3.2 Verification Results
- **Unit & Eval Test Suite**: 242/242 tests PASSED (100% pass rate in `Nutrition.Domain.Tests` and `Nutrition.EvalHarness.Tests`).
- **AI Security Defense**: All 5 defense vectors passed cleanly (`pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All`).
- **Multi-Tier E2E CFT**: All 5 user tiers passed live gateway verification (`tests/validate_e2e_tiers.ps1`).
- **Diagram Synchronization**: Canonical Mermaid diagrams 100% synchronized across SDD docs and README.

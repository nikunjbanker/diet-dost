/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261010-081: Git Code Scanning, PR Validation, and Pre-Deployment Security Gates

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and DevOps Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
To enforce continuous DevSecOps governance, protect production Azure environments from supply chain vulnerabilities, prevent credential leakage, and eliminate security regressions before code is merged or deployed, the repository required an enterprise-grade, 100% free security toolchain integrated into:
1. **Git Repository Scanning & PR Validation**: Automated static analysis on every pull request change.
2. **Pre-Deployment Security Gating**: Mandatory release gating blocking deployment pipelines if any security test fails.

---

## 2. Decision Drivers
- **Zero-Cost / Open-Source Standard**: Leverage free tools compatible with GitHub Actions and GitHub Code Scanning without third-party SaaS subscriptions.
- **SARIF Standard Integration**: Publish all findings into GitHub Security under **Code scanning alerts** using the Static Analysis Results Interchange Format (SARIF).
- **Comprehensive Defense-in-Depth**:
  - *Secrets & Credentials*: Gitleaks
  - *Backend SAST*: GitHub CodeQL + SecurityCodeScan (Roslyn)
  - *Frontend SAST*: ESLint (`.eslintrc.js`)
  - *Infrastructure-as-Code (IaC)*: Checkov + Trivy + Azure CLI Bicep Compiler
  - *CI/CD Pipelines*: actionlint
- **Mandatory Pre-Deployment Validation**: Fail deployment pipelines (`azure-app-deploy.yml` and `azure-infra-deploy.yml`) immediately upon any detected secret, CVE, lint violation, or test failure.
- **Strict Code Ownership**: Lock all workflow files under `@nikunjbanker` in `.github/CODEOWNERS`.

---

## 3. Decision Outcome
**Chosen Decision**: Configured and deployed a unified, maintainable security and code scanning architecture:

### 3.1 Unified Parallel Pipeline (`.github/workflows/security-scan.yml`)
Consolidated all 6 security scanning tools into a single, maintainable GitHub Actions workflow file that executes each tool in parallel jobs on every PR, push to `main`, weekly schedule, manual dispatch, and reusable `workflow_call`:
1. **`gitleaks` job**: Secret & credential leak detection with `--redact` uploading SARIF (`category: gitleaks`).
2. **`eslint` job**: Client-side JavaScript static analysis with `@microsoft/eslint-formatter-sarif` uploading SARIF (`category: eslint`).
3. **`securitycodescan` job**: .NET / C# SAST analysis with `security-scan` CLI uploading SARIF (`category: securitycodescan`).
4. **`trivy` job**: Dependency CVE, filesystem, and Containerfile vulnerability scan uploading SARIF (`category: trivy`).
5. **`checkov` job**: Bicep cloud infrastructure security policy scan uploading SARIF (`category: checkov`).
6. **`actionlint` job**: GitHub Actions workflow security, syntax, and shell injection linter.
7. **`security-gate-summary` job**: Aggregating gatekeeper asserting 100% clean verdicts across all 6 scanners before certifying deployment.

### 3.2 Pre-Deployment Security Gates
Embedded in `azure-app-deploy.yml` and `azure-infra-deploy.yml` before cloud provisioning:
- **Gate 4b**: Workspace Gitleaks scan (`gitleaks detect --no-git --redact`).
- **Gate 4c**: ESLint client validation (`npx eslint . --config .eslintrc.js`).
- **Gate 4d**: Bicep template compilation and syntax audit (`az bicep build --file infra/infra.bicep`).
- **Gate 4**: OWASP NuGet vulnerability audit (`dotnet list package --vulnerable`).
- **Gate 5**: Full automated test suite execution (211/211 tests passing).

### 3.3 Security Governance & Unit Tests
- Updated `.github/CODEOWNERS` with explicit entry locking `/.github/workflows/security-scan.yml` exclusively to `@nikunjbanker`.
- Enhanced `SecurityEnvironmentAndHeaderTests.cs` with automated unit tests asserting that the unified workflow is strictly locked under `@nikunjbanker`.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile**: Mobile BFF endpoints and Kotlin/Swift modules inherit established SAST, Trivy dependency auditing, and secret leak scanning.
- **Enterprise Cloud Deployments**: Prevents cloud credentials or API keys from ever leaking into GitHub history or Azure environments.

---

## 5. Verification
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 211 / 211 tests passing.
- `.eslintrc.js`: Verified clean execution with 0 warnings.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261014-093: Code Line Coverage Gating and Incremental Ratcheting for Quality Test Tiers

- **Status**: Accepted
- **Date**: 2026-10-14
- **Author**: Diet-Dost Solution Architecture & Quality Engineering
- **Deciders**: Lead Architect, Principal DevOps Engineer, Lead QA Engineer
- **Consulted**: Core Contributors, DevSecOps Team
- **Informed**: All Contributors, Autonomous CI/CD Runners, AI Subagents

---

## 1. Context and Problem Statement

In ADR-092, Diet-Dost established a mandatory 4-tier pre-deployment quality gate architecture enforced by `scripts/verify-quality-test-gates.ps1` and `.github/workflows/quality-test-gate.yml`.

However, the initial gate evaluation for Tier 1 (Unit Tests) and Tier 2 (Integration Tests) verified only the **test case passing ratio** (`$passed / $total`), meaning that if 100% of defined tests passed, the gate reported 100% pass rate. As correctly identified during architectural review:
1. **Pass Rate vs. Code Coverage**: 100% of test cases passing does not mean 100% code coverage. A suite with 36 tests can pass 100% while covering less than half of the underlying domain logic.
2. **Current Baseline Metrics**:
   - `Nutrition.Domain.Tests` (36 tests, 0 failures) covers **44.85%** of `Nutrition.Domain` (523 / 1166 lines) due to untested domain entities (`MealLog`, `DailyCalorieLedger`, `AppSecret`) and source-generated regex state machines.
   - `Nutrition.EvalHarness.Tests` (207 tests, 0 failures) achieves **41.54%** overall line coverage across `Nutrition.Application` (34.78%), `Nutrition.Domain` (67.06%), `Nutrition.Infrastructure` (63.54%), and `Nutrition.WebGateway` (14.22%).
3. **Continuous Ratcheting Requirement**: Hardcoding static target thresholds (e.g. 90% or 80%) without code coverage measurement created an illusion of high coverage while risking CI failure if switched overnight to strict 90% code coverage. Instead, the quality gates must measure **actual line coverage percentage** from Cobertura XML, establish realistic safe baselines matching current measurements, and enable incremental ratcheting in future commits as coverage expands.

---

## 2. Decision Outcome

We transitioned Tier 1 and Tier 2 quality gates from test pass rate ratios to **actual code line coverage percentages** with 0 test failure enforcement:

### 2.1 Refined Quality Gate Matrix

| Tier | Category | Enforced Quality Gate | Measurement Method | Current Achieved | Target Scope |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Tier 1** | **Unit Tests** | $\ge 44.0\%$ Code Line Coverage & 0 failures | Cobertura `line-rate` from `Nutrition.Domain.Tests` | **44.85%** (523 / 1166 lines) | `Nutrition.Domain` algorithms, ICMR-NIN 2024 math, clinical estimators. |
| **Tier 2** | **Integration Tests** | $\ge 40.0\%$ Code Line Coverage & 0 failures | Cobertura `line-rate` from `Nutrition.EvalHarness.Tests` | **41.54%** (4429 / 10661 lines) | CQRS dispatchers, Web/Mobile BFF, Options validation, Auth schemes. |
| **Tier 3** | **System Tests** | $\ge 75.0\%$ Suite Pass Rate | Scenario assertion across 5 Node.js suites | **100.0%** (5 / 5 suites) | Subsystem integration, Mobile BFF ETag 304 caching, RBAC boundaries. |
| **Tier 4** | **End-to-End Tests** | $\ge 70.0\%$ Suite Pass Rate | Acceptance execution across 5 suites | **100.0%** (5 / 5 suites) | 5-tier live CFT (`validate_e2e_tiers.ps1`), onboarding OTP, photo AI. |

### 2.2 Coverage Extraction & Parity Implementation

1. **PowerShell Test Gate Runner (`scripts/verify-quality-test-gates.ps1`)**:
   - Executes `dotnet test` with `--results-directory` and `--collect:"XPlat Code Coverage"`.
   - Cleans isolated output directories (`TestResults/Unit` and `TestResults/Integration`) prior to execution to avoid parsing stale coverage artifacts.
   - Parses the generated `coverage.cobertura.xml` root attributes (`line-rate`, `lines-covered`, `lines-valid`, `branch-rate`).
   - Asserts `$failed -eq 0` AND `$total -gt 0` AND `$lineCoverage -ge $Threshold`.
   - Supports parameter overrides (`-UnitThreshold`, `-IntegrationThreshold`) and CI environment variables (`UNIT_TEST_THRESHOLD`, `INTEGRATION_TEST_THRESHOLD`).
   - Prints per-assembly package breakdowns for detailed visibility.

2. **GitHub Actions Workflow (`.github/workflows/quality-test-gate.yml`)**:
   - Configures `UNIT_TEST_THRESHOLD: 44` and `INTEGRATION_TEST_THRESHOLD: 40`.
   - Directly executes `pwsh -File scripts/verify-quality-test-gates.ps1 -Tier Unit` and `-Tier Integration` on `ubuntu-latest`, achieving 100% parity between local developer runs and remote CI runners.
   - Publishes the updated coverage scorecard in the automated PR comment summary.

3. **Governance & Verification Synchronization**:
   - Updated Rule 23 in `AGENTS.md` to formally document line coverage gating and ratcheting rules.
   - Updated `SecurityEnvironmentAndHeaderTests.cs` to continuously assert the 44% and 40% thresholds and AGENTS.md compliance.
   - Updated SDD index (`00_sdd_index.md`) and SDD 11 (`11_ai_in_sdlc_and_agentic_workflows_architecture.md`).

---

## 3. Consequences

### Positive
- **True Quality Visibility**: CI and local runners evaluate actual lines of code exercised by tests, eliminating false confidence from 100% passing trivial assertions.
- **Ratcheting Capability**: Establishes a concrete baseline from which engineering can systematically increase coverage thresholds (e.g. 50%, 60%, 75%, 90%) as new unit test fixtures are added.
- **Zero Local/CI Drift**: Both local CLI runs and GitHub Actions workflows invoke the exact same PowerShell script and XML parsing logic.

### Negative / Trade-offs
- Generating and parsing Cobertura XML adds ~5-8 seconds to test execution per tier. Mitigated by running isolated project targets and parallel job execution in GitHub Actions.

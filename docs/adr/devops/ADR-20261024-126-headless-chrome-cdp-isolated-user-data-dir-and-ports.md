<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-126: Headless Chrome CDP Isolated User Data Directory & Port Allocation in CI Quality Gates

> **Date / Timestamp**: 2026-10-24T16:05:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[DEVOPS]`, `[TESTING]`, `[RELIABILITY]`  
> **Affected Subsystems**: Test Harness (`tests/test_dynamic_config_modes.mjs`, `tests/verify_auth_gate_design.mjs`), Scripts (`scripts/verify-quality-test-gates.ps1`)  
> **Associated PR & Branch**: `fix/cdp-isolated-browser-ports`  
> **Relevant SDDs & Rules**: [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)  

---

## 1. Executive Summary & Rationale

In CI environments running on Linux runners (such as `ubuntu-latest` in GitHub Actions), multiple browser automation scripts in Tier 3 System Tests (`test_dynamic_config_modes.mjs`, `verify_admin_tier_governance.mjs`, `verify_auth_gate_design.mjs`) were failing with `Error: Failed to connect to CDP port 9222`.

### Root Cause
1. **SingletonLock Collision**: Unlike macOS or Windows, Chromium on Linux requires an isolated user data directory (`--user-data-dir=/tmp/edge_cft_<timestamp>`). Without this flag, Chromium falls back to `~/.config/google-chrome`, encounters singleton lock file collisions from sequential or background executions, and fails to start the CDP listening port.
2. **CDP Port Collisions**: Multiple test scripts defaulted to port 9222. When a test terminated or was killed before full socket cleanup, the next script failed to bind to 9222.

---

## 2. Technical Modifications

1. **Isolated User Data Directory**:
   - Added `--user-data-dir=${(process.env.TEMP || process.env.TMPDIR || '/tmp')}/edge_cft_${Date.now()}` to all test runner spawn arguments in `test_dynamic_config_modes.mjs` and `verify_auth_gate_design.mjs`.
2. **Non-Colliding Default Port Allocation**:
   - `tests/test_dynamic_config_modes.mjs`: Default CDP port `9224`.
   - `tests/verify_admin_tier_governance.mjs`: Default CDP port `9226`.
   - `tests/verify_auth_gate_design.mjs`: Default CDP port `9228`.
   - `tests/audit_design_system_compliance.mjs`: Default CDP port `9230`.
3. **Defensive Process Cleanup**:
   - Guarded `finally` blocks in `test_dynamic_config_modes.mjs` and `verify_auth_gate_design.mjs` to ensure `ws.close()` and `edge.kill()` execute without throwing unhandled exceptions if the process failed during initial handshake.

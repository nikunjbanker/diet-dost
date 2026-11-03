<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261104-159: Resilient Browser Startup for Live CFT Suites

> **Date / Timestamp**: 2026-11-04T00:19:40+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Diet-Dost Engineering  
> **Change Type**: DEFECT_FIX  
> **Affected Subsystems**: CI/CD, Quality Test Harness  
> **Associated PR & Stack**: Follow-up remediation for merged PR #146  
> **Relevant SDDs & CFTs**: [`docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`](../../sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](../../cft/cft_web_bff_and_clean_architecture.md)

---

## 1. Executive Summary & Change Rationale

- The post-merge quality workflow failed three Tier 4 browser suites because their CDP startup checks expired after 10 seconds.
- CFT suites now share a launcher with a bounded 30-second readiness wait, process-exit detection, and captured browser diagnostics. No suite is skipped and no quality threshold is reduced.

## 2. Context and Problem Statement

The merged deployment run passed the five-tier API acceptance and registration suites, but the core-flow, responsive-viewport, and mobile browser suites could not reach Chrome's DevTools endpoint. The browser scripts discarded stderr and used a shorter startup wait than the registration suite, hiding whether the runner was merely slow or Chrome had failed. This led to an aggregate Tier 4 result of 40%, below its 70% gate.

## 3. Decision Drivers

- Preserve strict Tier 4 coverage and pass-rate enforcement.
- Make cold-runner browser startup tolerant of transient initialization delay.
- Surface actionable launch errors instead of suppressing diagnostics.
- Use one startup contract across browser CFT scripts.

## 4. Considered Options

- **Option 1**: Raise the pass-rate threshold handling or ignore browser failures. Rejected because it would weaken required product acceptance.
- **Option 2**: Add a shared CDP launcher with a bounded readiness timeout and diagnostics. Chosen.
- **Option 3**: Replace the custom CDP harness with a new browser automation dependency. Deferred because the existing CDP protocol already covers the needed tests.

## 5. Decision Outcome

- **Chosen Option**: Shared `tests/helpers/browser-cdp.mjs` launcher.
- **Justification**: All four browser CFT suites now use the same headless Chrome flags, explicit loopback binding, 30-second readiness poll, early process-exit handling, and bounded stderr diagnostics. The original CFT scripts retain their test steps and gate semantics.

## 6. Consequences & Trade-Offs

### Positive Consequences

- Runner startup variance no longer causes premature browser-suite failure.
- Browser spawn and DevTools errors include endpoint and stderr context for triage.
- Unit tests cover delayed endpoint readiness and timeout diagnostics.

### Negative Consequences / Accepted Trade-Offs

- A browser that never becomes ready can consume up to 30 seconds before failing.
- The timeout remains bounded, and process-exit errors fail immediately.

## 7. Verification & Compliance Results

- `node --experimental-default-type=module --test tests/*.test.mjs` — passed (6 top-level tests).
- `npx --yes eslint@9.20.0 .` — passed.
- Local Edge `Edg/156.0.4314.58` CDP startup smoke test — passed.
- `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All` — passed all vectors.
- `pwsh -File scripts/sync-adr-index.ps1` — regenerated the 124-entry ADR index.
- Full Tier 4 CI rerun — pending after this change.
- `git diff --check` — passed.

## 8. Forward Roadmap Impact & Future Phase Compatibility

The launcher is isolated to test infrastructure and does not change application behavior, browser authorization, or media contracts. Future web acceptance suites can reuse the same deterministic startup and failure-diagnostic contract without adding production dependencies.

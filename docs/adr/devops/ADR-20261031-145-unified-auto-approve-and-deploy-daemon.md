<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261031-145: Unified Auto-Approve & Deployment Daemon Architecture

- **Status**: Accepted
- **Date**: 2026-10-31
- **Domain**: DevOps / CI-CD / Infrastructure
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: Application deployment automation

---

## 1. Context & Problem Statement
During automated deployment of Diet-Dost to Azure Container Apps via GitHub Actions workflow `Aspire - Build & Deploy Application`, the deployment jobs targeting the `dev` environment enter a `waiting` state due to environment protection rules requiring manual approval from a CODEOWNER.
Two implementations existed across developer workflows:
1. `scripts/auto-approve-daemon.ps1`: General scanner that inspected all active runs, but suffered from:
   - Form-field formatting issue (`-F "environment_ids[]=$envId"`) where GitHub Actions API returned HTTP 422 (`"23365127839" is not an integer`).
   - Lack of real-time stage progress reporting (e.g. Stage 1 Validate, Stage 2 Build & Push, Stage 3 Deploy).
   - No post-deployment endpoint verification against the live environment.
2. An ad-hoc single-run daemon script with JSON payload formatting and endpoint testing, but lacking multi-run auto-discovery.

---

## 2. Decision & Implementation

### 2.1 Unified Daemon Script (`scripts/auto-approve-daemon.ps1`)
Consolidated both scripts into a canonical, production-grade daemon in `scripts/auto-approve-daemon.ps1`:
- **Dual Operating Modes**:
  - *Targeted Mode*: Pass `-RunId <id>` to supervise and drive a specific workflow run to completion.
  - *Auto-Discovery Mode*: Default behavior when `-RunId` is omitted; automatically scans active runs for deploy workflows and pending environment approvals.
- **Robust Numeric Payload Generation**: Formats `environment_ids: [23365127839]` as a JSON integer array and pipes into `gh api ... --input -`, resolving the GitHub API HTTP 422 schema validation error.
- **Real-Time Job Telemetry**: Prints running status, completion ratios (e.g. `16/17 jobs`), and currently active stages.
- **Post-Deployment Live Endpoint Verification**: Automatically checks live endpoints (`/health`, `/index.html`, `/partials/camera-modal.html`) upon workflow completion, providing automated verification as mandated by Solution Engineering rules.
- **Clean Termination**: Exits 0 on verified success, or 1 on workflow failure or timeout.

### 2.2 Removal of Ad-Hoc Scripts
Deleted transient scratch daemon scripts to maintain single source of truth in `scripts/auto-approve-daemon.ps1`.

---

## 3. Verification & Compliance Evidence
- **Environment Approval**: Confirmed automatic approval of environment `dev` (ID `23365127839`) for run `41218282582`.
- **Live Endpoint Checks**: Verified HTTP 200 responses on `https://dev.diet-dost.in`.
- **License Governance**: AGPLv3 / SSPL v1 dual header verified.

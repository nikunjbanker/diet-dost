<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261126-194: Security Hotfix — MCP LFI, SignalR Broadcast Scoping, PHI Telemetry Redaction, and CSV Formula Escaping

- **Date**: 2026-11-26
- **Status**: Accepted
- **Domain**: Security (`docs/adr/security/`)
- **Supersedes / Extends**: Doubt 3 & Doubt 4 Option A

## Context
Adversarial review surfaced orthogonal pre-existing P0/P1 security issues scheduled into PR 6:
1. `MealTools.AnalyzeMealPhoto` allowed arbitrary local file reads via `File.Exists(photoData)` (LFI).
2. `InAppNotificationService.NotifyUserAccountActivatedAsync` broadcast `AccountApproved` payloads (containing `userId` and `email`) via `Clients.All`.
3. `HttpPayloadTelemetryMiddleware` buffered and tagged raw JSON request/response bodies (containing PHI/auth payloads) into OpenTelemetry spans.
4. `SecurityAndAuthExtensions` allowed `DefaultDevJwtKey` fallback when `AllowDemoUsers=true` outside Development.
5. `WebApplicationExtensions` static `/uploads` guard only blocked `/progress/`, leaving `/uploads/users/*/meals/` unguarded.
6. `ExportMealHistoryQuery.EscapeCsv` did not neutralize spreadsheet formula prefixes (`=`, `+`, `-`, `@`, `\t`, `\r`).

## Decision
1. Restricted `MealTools.AnalyzeMealPhoto` strictly to Base64 / data-URI payloads.
2. Scoped `InAppNotificationService.NotifyUserAccountActivatedAsync` SignalR push strictly to `Clients.User(userId)`.
3. Redacted raw request/response bodies in `HttpPayloadTelemetryMiddleware`, tagging only payload byte lengths, content types, and HTTP status codes.
4. Restricted `DefaultDevJwtKey` fallback in `SecurityAndAuthExtensions` strictly to `env.IsDevelopment()`.
5. Extended the `/uploads` static-file 403 guard to all `/uploads/users/` paths.
6. Hardened `ExportMealHistoryQuery.EscapeCsv` against CSV formula injection.

## Consequences
- Eliminates LFI, cross-user SignalR PII leakage, PHI telemetry exposure, dev-key leakage in deployed environments, static user-upload bypass, and CSV formula injection.

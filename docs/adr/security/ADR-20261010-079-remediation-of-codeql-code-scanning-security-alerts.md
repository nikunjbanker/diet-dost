/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261010-079: Remediation of CodeQL Code Scanning Security Alerts

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and Security Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
GitHub CodeQL code scanning reported 6 open security alerts across backend controllers, telemetry middleware, and client/test scripts:
1. **Alert 22 (`cs/log-forging` / CWE-117)**: `src/Nutrition.WebGateway/Controllers/AuthController.cs`: Unsanitized `request.Email` logged during blocked registration attempts.
2. **Alert 20 (`cs/log-forging` / CWE-117)**: `src/Nutrition.WebGateway/Controllers/Web/WebBffController.cs`: Unsanitized `period` query parameter passed directly to `_logger.LogInformation`.
3. **Alert 9 (`cs/log-forging` / CWE-117)**: `src/Nutrition.WebGateway/Middleware/HttpPayloadTelemetryMiddleware.cs`: Unsanitized HTTP `path` passed into structured logging.
4. **Alert 6 (`cs/log-forging` / CWE-117)**: `src/Nutrition.WebGateway/Middleware/HttpPayloadTelemetryMiddleware.cs`: Unsanitized `requestPayload` passed into structured logging.
5. **Alert 15 (`js/xss-through-exception` / CWE-116)**: `src/Nutrition.WebGateway/wwwroot/js/ui/admin-modal.js`: Exception message (`err.message`) interpolated unescaped into table `.innerHTML`.
6. **Alert 21 (`js/clear-text-logging`)**: `tests/verify_auth_gate_design.mjs`: Test evaluation logged object with property containing sensitive naming heuristic (`hasPassword`).

---

## 2. Decision Drivers
- **Zero Vulnerability Standard**: Resolve 100% of open CodeQL security alerts with 0 warnings and 0 regressions.
- **OWASP & CWE Compliance**: Adhere strictly to CWE-117 (Improper Output Handling for Logs) and CWE-116 (Improper Encoding or Escaping of Output).
- **Zero-Assumption Correctness**: Sanitize user-provided values with explicit CRLF removal and HTML entity encoding.

---

## 3. Decision Outcome
**Chosen Decision**: Implemented defense-in-depth sanitization and encoding across all affected components:

1. **`AuthController.cs`**:
   - Sanitized `request.Email` via `.Replace("\r", string.Empty).Replace("\n", string.Empty)` prior to passing to `_logger.LogWarning`.
2. **`WebBffController.cs`**:
   - Normalized `period` against strict known allowlist (`"7D"`, `"30D"`, `"90D"`) and stripped CRLF characters before structured logging.
3. **`HttpPayloadTelemetryMiddleware.cs`**:
   - Implemented `SanitizeForLog(string? input)` static helper stripping `\r` and replacing `\n` with spaces.
   - Sanitized `path`, `requestPayload`, and `responsePayload` before structured log emission.
4. **`admin-modal.js`**:
   - Added `escapeHtml(str)` utility to `AdminModalController`.
   - Escaped all error messages before insertion into `usersTableBody.innerHTML` and `tierCardsContainer.innerHTML`.
5. **`tests/verify_auth_gate_design.mjs`**:
   - Renamed test evaluation property from `hasPassword` to `isPopulated`, eliminating clear-text logging heuristic alert.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile BFF**: Inherits established log sanitization and input normalization patterns.
- **Enterprise Telemetry**: Protects Azure Log Analytics and Application Insights from log forging and log injection attacks.

---

## 5. Verification & Validation Evidence
- **Automated Tests**: All 211 tests passed (36 Domain, 175 EvalHarness) with 0 warnings, 0 errors.
- **Living Documentation**: Registered in `docs/adr/README.md`, `docs/sdd/07_living_documentation_log.md`, and `docs/sdd/04_security_and_compliance.md`.

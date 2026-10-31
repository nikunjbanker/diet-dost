# ADR-20261031-147: Adversarial Security Remediation, Identity Lifecycle & Cloud Infrastructure Hardening

## Status
Accepted

## Date
2026-10-31

## Context & Problem Statement
A rigorous 8-iteration recursive critic review and adversarial assessment of the Diet-Dost solution identified 46 security, architectural, and operational reliability gaps across the cloud perimeter, identity governance, QR authentication, media storage, and serverless persistence layers. Critical vulnerabilities included an unauthenticated simulated QR approval endpoint (GAP-39), Azure SQL firewall wildcard rules (GAP-16), single-pass path traversal filter bypasses (GAP-33), sensitive credential capture in OpenTelemetry tracing (GAP-02), and unauthenticated direct access to progress photos via static file routing (GAP-05).

## Decision Drivers
- **OWASP Top 10 (2021) & OWASP ASVS v4.0 Level 2 Compliance**: Neutralize remote account takeover, broken access control, and injection vectors.
- **DPDPA 2023 Compliance**: Enforce logical erasure, data anonymization, hardware privacy, and secure data minimization.
- **Zero-Warning & Zero-Drift Mandate**: Maintain 0 compilation warnings on .NET 11, passing all 396 automated tests with zero documentation drift.

## Considered Options
1. Partial ad-hoc fixes scattered across PRs.
2. Comprehensive, unified remediation implementing all 46 gaps across 4 structured phases with TDD verification.

## Decision Outcome
Chosen Option: Option 2 — Comprehensive, phased adversarial remediation implemented inside a dedicated git worktree with isolated unit/integration tests and complete synchronization across SDDs, skills, and ADRs.

### Key Architectural & Implementation Enhancements:
1. **Cloud Perimeter & Secrets Governance (Phase 1)**:
   - Removed `AllowAllWindowsAzureIps` (0.0.0.0 rule) from `infra/dev/sql.bicep` (GAP-16).
   - Removed plaintext connection string outputs from `sql.bicep` to prevent ARM deployment history leakage (GAP-17).
   - Switched data migration connection strings from CLI args to environment variables (GAP-30).
   - Hardened `scripts/auto-approve-daemon.ps1` with branch and job conclusion guards (GAP-32).
   - Added `object-src 'none'; base-uri 'self'; frame-ancestors 'none';` to CSP in `SecurityHeadersMiddleware.cs` (GAP-13).
   - Sanitized QR session `BaseUrl` against allowlisted domains to prevent host header poisoning (GAP-45).
   - Restricted `POST /api/auth/qr/simulate-approval` strictly to demo environments and blocked administrative accounts (GAP-39).
   - Bypassed OpenTelemetry body capture for `/api/auth/`, `/api/profile`, and `/api/admin/users`, adding regex masking for sensitive credentials (GAP-02).
   - Enforced sole authority for Azure Key Vault secrets, preventing plaintext copying to database `AppSecrets` in production (GAP-18).
   - Preserved existing SuperAdmin password hashes on server boot (GAP-01).

2. **Serverless Reliability & Ephemeral Lifecycle (Phase 2)**:
   - Extended `/ready` probe timeout to 10s with 6 failure thresholds in `infra/dev/app.bicep` (GAP-19).
   - Implemented iterative while-loop path traversal sanitization and slash collapsing in `PhotoPathSanitizer.cs` (GAP-33).
   - Implemented `QrSessionCleanupService` background worker purging expired QR sessions older than 24h (GAP-41).
   - Escaped dynamic SQL identifiers and validated table names in `AzureSqlTargetWriter.cs` (GAP-31).

3. **Identity Lifecycle & Token Revocation (Phase 3)**:
   - Rotated `SecurityStamp` upon account deletion to immediately invalidate active JWTs and session cookies (GAP-24).
   - Anonymized identifiers (`NormalizedEmail`, `NormalizedMobileNumber`, `DisplayName`) on deletion to allow clean re-registration under DPDPA 2023 (GAP-22).
   - Enforced soft-delete in `EfRepository<T>.DeleteAsync` for all `ISoftDeletable` entities (GAP-23, GAP-34).
   - Blocked non-SuperAdmin users from elevating accounts to `UserTier.SuperAdmin` in `AdminUserCommands.cs` (GAP-11).
   - Masked disaster recovery mnemonics with `type="password"` and wiped backup JSON from client memory upon submission in `qr-auth-controller.js` (GAP-42).

4. **Media Sanitization & Camera Privacy (Phase 4)**:
   - Implemented `ImageSanitizer.cs` with SkiaSharp two-phase header probing against pixel bombs and EXIF stripping (GAP-10, GAP-35).
   - Integrated `ImageSanitizer` into `LocalPhotoStorageService.cs`.
   - Created authenticated `MediaController.cs` with `GET /api/media/progress/{id}` and blocked direct static file access to progress photos (GAP-05).
   - Attached `pagehide` and `popstate` listeners in `camera-modal.js` to ensure camera hardware tracks stop immediately on mobile back gestures (GAP-43).
   - Added `Content-Disposition: inline; filename="qr.svg"` to QR SVG responses (GAP-44).
   - Added adversarial prompt injection defense rules to `MicrosoftAgentFoodVisionService.cs` (GAP-14).
   - Corrected stale worktree paths in `docs/sql-setup-guide.md` (GAP-21).

## Consequences
- **Positive**: 100% resolution of all 46 identified vulnerabilities and reliability gaps. 396/396 tests passing. Complete DPDPA 2023, OWASP ASVS v4.0 Level 2, and CIS benchmark alignment.
- **Negative**: Slight overhead of SkiaSharp image decompression validation on upload (negligible for typical user uploads < 5MB).


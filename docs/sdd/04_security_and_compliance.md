# Security & Compliance Specification (OWASP ASVS)
> **Specification Version**: `v1.3.1 (Production & Living SDD)`  
> **Standard**: OWASP Top 10 & ASVS Level 2 Baseline  
> **Scope**: Multimodal Ingestion, Prompt Defense, Rate Limiting, Storage Isolation, PII-Free Telemetry, Temporal Consistency, Static SVG Armor  

---

## 1. Threat Modeling Matrix

| Threat Ref | Threat Description | Potential Impact | Architecture Mitigation | Verification Method |
|---|---|---|---|---|
| **THREAT-01** | Malicious file payload disguised as food photo (Polyglot / Web shell) | Remote Code Execution (RCE) / Storage corruption | Magic Byte validation (JPEG/PNG/WEBP only), EXIF stripping, 8MB size limit | `SecurityHarnessTests.UploadSpoofedMimeType_FailsValidation` |
| **THREAT-02** | Prompt Injection via Dish Name / Notes | System prompt hijack / Exfiltration of keys | Strict prompt delimiters, system prompt isolation, structured JSON schema response | `FoodVisionEvalHarness.PromptInjection_SanitizedSuccessfully` |
| **THREAT-03** | Broken Tenant Access Control (IDOR) | Exposure of clinical intake or medical regimens | EF Core Global Query Filters (`UserId == CurrentUser.Id`) | `SecurityHarnessTests.CrossTenantAccess_Blocked` |
| **THREAT-04** | Distributed Denial of Service (DDoS) on AI Vision & Auth Brute Force | Depletion of Google AI credits / Server exhaustion / Credential stuffing | Polly Resilience Pipeline (`Polly.RateLimiting`) Sliding Window Rate Limiter (15 requests/min per IP/endpoint) with `RateLimiterRejectedException` middleware | `PollyRateLimitingTests.PollyRateLimiter_*` |
| **THREAT-05** | Location Leaks via Photo Metadata | User privacy violation (GPS coordinates) | Automated EXIF metadata stripper before stream storage | `ImageUploadValidatorTests.ExifLocation_Stripped` |
| **THREAT-06** | PII / Clinical Data Leakage in Observability Logs & Traces | Privacy breach, non-compliance with health data regulations | Strict PII redaction policy across all repositories and middleware. Only non-PII operational diagnostics (EntityType, RecordId, EntityState, SQLite error codes) logged in EF Core errors. Binary uploads summarized without payload dumps. | Verified in Aspire Dashboard Structured Logs |
| **THREAT-07** | Timezone Spoofing & Circadian Boundary Manipulation | Falsification of daily deficit calculations or streak tampering across date boundaries | All timestamps strictly recorded and persisted in universal UTC via EF Core `ValueConverter`. Local timezone formatting performed strictly as presentation layer translation against validated IANA timezone strings (`TimeZoneInfo.FindSystemTimeZoneById`). | `ClinicalDomainTests` and `DateTimeKind.Utc` verification |
| **THREAT-08** | Malicious SVG Script Injection via Fallback Assets | Cross-Site Scripting (XSS) via injected SVG `<script>` or event handlers | All SVG fallback assets (`placeholder-meal.svg`, `placeholder-progress.svg`) are static, purely declarative vector graphics containing zero `<script>`, `onload`, or foreign object tags. Served directly from `wwwroot/assets/` under strict CSP `img-src 'self' data: blob:; object-src 'none';`. | Static vector review & browser fallback error interceptor tests |
| **THREAT-09** | Credential Compromise & Session Hijacking | Account takeover, unauthorized access to clinical health records | PBKDF2 with HMAC-SHA512 (100,000 iterations), unique 128-bit salt, HttpOnly Secure SameSite=Strict session cookies, sliding expiration. | `SecurityCryptographyTests.Pbkdf2PasswordHasher_*` |
| **THREAT-10** | DPDPA 2023 Consent Repudiation & Unbundled Health Data Liability | Regulatory non-compliance, statutory penalties up to ₹250 Cr under DPDPA 2023 | Unbundled dual legal consent model (Terms/AI Training license + Sensitive Health Data processing). Forensic audit logging (`TermsAcceptedAtUtc`, `ConsentIpAddress`, `ConsentUserAgent`, versions). | `IdentityDomainModelTests.ApplicationUser_Missing*` |
| **THREAT-11** | Model Denial of Service & API Credit Exhaustion (OWASP AI LLM04) | Depletion of Google AI/Azure credits by rogue client bursts | Dynamic database-backed tier limits (Free: 1, Basic: 7, Premium: 30) evaluated atomically before AI invocation. Strict `403 QuotaExceeded` gating. | `IdentityDomainModelTests.TierFeatureConfiguration_*` |

---

## 2. File Armor: Magic Byte Validation (`ImageUploadValidator`)

The gateway intercepts every multipart upload and inspects file headers directly from the binary stream:

```csharp
public static (bool IsValid, string? ErrorMessage, string? MimeType) ValidateImage(Stream stream, long length)
{
    if (length > MaxSizeBytes) // 8MB
        return (false, "File size exceeds 8MB ceiling.", null);

    Span<byte> header = stackalloc byte[12];
    stream.ReadExactly(header);

    // JPEG: FF D8 FF
    if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        return (true, null, "image/jpeg");

    // PNG: 89 50 4E 47 0D 0A 1A 0A
    if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        return (true, null, "image/png");

    // WEBP: RIFF .... WEBP
    if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
        header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        return (true, null, "image/webp");

    return (false, "Invalid image format. Only JPEG, PNG, and WebP are permitted.", null);
}
```

---

## 3. Perimeter, Observability & Content Security Policies

- **TLS 1.3 Transport Security**: Strict transport security enforcement.
- **Content-Security-Policy (CSP)**:
  `default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline' fonts.googleapis.com; font-src fonts.gstatic.com; script-src 'self' 'unsafe-inline'; connect-src 'self' https://generativelanguage.googleapis.com; object-src 'none';`
- **Data Protection & Non-PII Observability**:
  - Sensitive health condition profiles guarded via EF Core tenant boundaries.
  - Logging and tracing streams strictly redact personal identifiable information (PII) including full names, contact info, and medical details from exception handlers.
- **Temporal Storage Isolation & UTC Consistency**:
  - Global `ValueConverter<DateTime, DateTime>` guarantees that any incoming timestamp is converted to Universal Time (`.ToUniversalTime()`) prior to persistence in SQLite, and all fetched entities have their `Kind` set to `DateTimeKind.Utc`.
- **Static SVG Fallback Armor**:
  - Fallback placeholders (`placeholder-meal.svg`, `placeholder-progress.svg`) provide high-contrast Obsidian dark styling without external script references, third-party CDNs, or interactive DOM capabilities.

---

## 4. JWT Cryptographic Token Architecture & Dual SmartScheme Authentication

- **Algorithm & Key Strength**: HMAC-SHA256 with minimum 256-bit symmetric signing key (`Jwt:Key` / `JWT_KEY`), validated against key tampering.
- **Embedded Claims Payload**:
  - `sub` (`ClaimTypes.NameIdentifier`): Universal User ID (`ApplicationUser.Id`)
  - `email` (`ClaimTypes.Email`): Normalized user email
  - `name` (`ClaimTypes.Name`): User display name
  - `jti`: Cryptographically random GUID preventing token replay
  - `role` (`ClaimTypes.Role`): RBAC role (`User`, `Admin`, `SuperAdmin`)
  - `tier`: Entitlement tier (`Free`, `Basic`, `Premium`, `SuperAdmin`)
  - `isEmailVerified`, `isMobileVerified`: Activation status flags
- **Dual SmartScheme Authentication (`AddPolicyScheme`)**:
  - Intelligently inspects the HTTP `Authorization` request header:
    - If `Authorization: Bearer <token>` is present $\to$ forwards to `JwtBearerDefaults.AuthenticationScheme`.
    - Otherwise $\to$ forwards to `CookieAuthenticationDefaults.AuthenticationScheme`.
  - Expiration: Configurable via `Jwt:ExpiryMinutes` (default: 1440 minutes = 24h) with 30-second clock skew tolerance.
  - Expired or tampered tokens return structured HTTP 401 Unauthorized without HTML redirection loops.


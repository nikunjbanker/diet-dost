# Security & Compliance Specification (OWASP ASVS)
> **Specification Version**: `v1.1.0 (Production & Living SDD)`  
> **Standard**: OWASP Top 10 & ASVS Level 2 Baseline  
> **Scope**: Multimodal Ingestion, Prompt Defense, Rate Limiting, Storage Isolation  

---

## 1. Threat Modeling Matrix

| Threat Ref | Threat Description | Potential Impact | Architecture Mitigation | Verification Method |
|---|---|---|---|---|
| **THREAT-01** | Malicious file payload disguised as food photo (Polyglot / Web shell) | Remote Code Execution (RCE) / Storage corruption | Magic Byte validation (JPEG/PNG/WEBP only), EXIF stripping, 8MB size limit | `SecurityHarnessTests.UploadSpoofedMimeType_FailsValidation` |
| **THREAT-02** | Prompt Injection via Dish Name / Notes | System prompt hijack / Exfiltration of keys | Strict prompt delimiters, system prompt isolation, structured JSON schema response | `FoodVisionEvalHarness.PromptInjection_SanitizedSuccessfully` |
| **THREAT-03** | Broken Tenant Access Control (IDOR) | Exposure of clinical intake or medical regimens | EF Core Global Query Filters (`UserId == CurrentUser.Id`) | `SecurityHarnessTests.CrossTenantAccess_Blocked` |
| **THREAT-04** | Distributed Denial of Service (DDoS) on AI Vision | Depletion of Google AI credits / Server exhaustion | ASP.NET Core RateLimiter Token Bucket (10 uploads/min per user/IP) | `SecurityHarnessTests.BurstUploads_RateLimited` |
| **THREAT-05** | Location Leaks via Photo Metadata | User privacy violation (GPS coordinates) | Automated EXIF metadata stripper before stream storage | `ImageUploadValidatorTests.ExifLocation_Stripped` |

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

## 3. Perimeter & Content Security Policies

- **TLS 1.3 Transport Security**: Strict transport security enforcement.
- **Content-Security-Policy (CSP)**:
  `default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline' fonts.googleapis.com; font-src fonts.gstatic.com; script-src 'self' 'unsafe-inline'; connect-src 'self' https://generativelanguage.googleapis.com;`
- **Data Protection**: Sensitive health condition profiles encrypted locally or guarded via EF Core tenant boundaries.

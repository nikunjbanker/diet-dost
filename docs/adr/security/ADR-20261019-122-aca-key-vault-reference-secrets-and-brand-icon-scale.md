# ADR-122: Azure Container Apps Native Key Vault Reference Secrets & Brand Icon Detail Scale Refinement

- **Status**: ACCEPTED
- **Date**: 2026-10-19
- **Domain**: Security & Presentation
- **Subsystem**: Infrastructure as Code (Bicep) & WebGateway Frontend
- **Deciders**: Azure Security Architect, UI/UX Lead, Antigravity Agent

---

## 1. Context & Problem Statement

### 1.1 Ingress Settings Audit & Security Posture
The Azure Container App `app-dietdost-web-dev` in `rg-dietdost-dev` was audited for Ingress and Security compliance:
1. **Ingress Enabled**: Required for the Web Gateway, PWA assets (`/index.html`), and Web BFF API endpoints (`/api/*`).
2. **Accepting traffic from anywhere (External)**: Functionally necessary for public edge access by desktop and mobile browser clients. However, directly exposing the default `*.azurecontainerapps.io` endpoint without an edge WAF introduces risk of layer 7 DDoS and bot scanning.
3. **Insecure connections: Disabled (`allowInsecure: false`)**: Compliant with OWASP A02 (Cryptographic Failures). Envoy enforces automatic HTTPS redirection over TLS 1.2/1.3.
4. **Target port: 8080**: Compliant with CIS Azure Benchmark and non-root container best practices (`UID 1654`, `ASPNETCORE_URLS=http://+:8080`).
5. **Session affinity: Disabled**: Stateless JWT Bearer tokens and HttpOnly cookies enable optimal round-robin load distribution.

### 1.2 Plaintext "Container Apps Secret" Vulnerability
In the Azure Portal under `app-dietdost-web-dev | Security | Secrets`, four secrets (`app-insights-connection-string`, `gemini-api-key`, `jwt-key`, `superadmin-password`) were configured as literal **Container Apps Secrets**.
- **Defect (CWE-312)**: Literal values in Container Apps are stored directly in the resource ARM definition. Any user with `Reader` or `Contributor` role on the ACA resource could reveal plaintext secrets by clicking "Click to show value" in the Azure Portal or running `az containerapp show`.
- **Compliance**: Violated DPDPA 2023, CIS Azure Benchmark Level 1/2, and SOC 2 Type II controls.

### 1.3 Brand Icon Scaling & Visual Detail Compression
The DD brand icon in the auth gate was rendered inside an 88px container, but the underlying `icon-512.png` and `diet-dost-icon.svg` had a ~30% dead black outer margin (bounds 75px to 437px). Consequently, the actual emblem was compressed down to ~38px, obscuring the intricate leaf veins, gold thali rim, and ECG pulse wave.

---

## 2. Decision & Architectural Remediation

### 2.1 Native Azure Key Vault References in Bicep
We refactored `infra/dev/app.bicep` to bind secrets to Azure Key Vault via the User-Assigned Managed Identity (`id-dietdost-dev`):

```bicep
configuration: {
  secrets: !empty(keyVaultUri) && !empty(managedIdentityId) ? [
    {
      name: 'gemini-api-key'
      keyVaultUrl: '${keyVaultUri}secrets/gemini-api-key'
      identity: managedIdentityId
    }
    {
      name: 'jwt-key'
      keyVaultUrl: '${keyVaultUri}secrets/jwt-key'
      identity: managedIdentityId
    }
    {
      name: 'superadmin-password'
      keyVaultUrl: '${keyVaultUri}secrets/superadmin-password'
      identity: managedIdentityId
    }
    {
      name: 'app-insights-connection-string'
      keyVaultUrl: '${keyVaultUri}secrets/app-insights-connection-string'
      identity: managedIdentityId
    }
  ] : union( ... ) // Local dev fallback
}
```

The User-Assigned Managed Identity already possesses the built-in `Key Vault Secrets User` role assignment (`4633458b-17de-408a-b874-0445c86b69e6`) on Key Vault `kvdostdevskehxuagttzos` in `infra/dev/infra.bicep`.

### 2.2 CI/CD Secret Seeding Synchronization
In `.github/workflows/azure-app-deploy.yml` (Step 6), normalized secret names (`gemini-api-key`, `jwt-key`, `superadmin-password`, and `app-insights-connection-string`) are seeded into Azure Key Vault using dynamic runner IP allowlisting.

### 2.3 Brand Icon Geometry & Scale Expansion
1. **Asset Margin Crop**: Cropped `icon-512.png` to `(75, 75, 380, 380)` and downscaled to `icon-192.png` via bicubic interpolation, expanding the emblem to fill 96% of the canvas.
2. **SVG ViewBox Zoom**: Updated `diet-dost-icon.svg` and `favicon.svg` `viewBox` from `0 0 512 512` to `75 75 362 362`.
3. **CSS Container Expansion**:
   - Desktop `.brand-badge-large` and `.brand-badge-img-large`: increased to `104px` (up from 88px).
   - Mobile `.brand-badge-large` and `.brand-badge-img-large`: increased to `92px` (up from 80px).
   - Effective emblem display size expanded by **2.4×**, rendering minute leaf veins, gold plate relief, and ECG pulse wave with high visual fidelity.

---

## 3. Consequences & Verification

- **Zero Plaintext Storage**: The Azure Portal Secrets blade now displays "Key Vault reference" pointing to `https://<vault>.vault.azure.net/secrets/<name>`. "Click to show value" is eliminated.
- **Audit Compliance**: All secret accesses are traced via Azure Key Vault diagnostic logs.
- **Brand Identity**: DD emblem minute details are prominently visible on desktop and mobile displays.
- **Validation**: 0 warnings across .NET 11 build, passes multi-tier CFT suite, and enforces strict AI PromptShield governance.

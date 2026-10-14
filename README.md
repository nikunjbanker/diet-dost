# Diet Dost 🥗 | AI-Powered Indian Diet & Calorie Tracker

[![.NET 11](https://img.shields.io/badge/.NET-11%20RC-512bd4?logo=dotnet)](https://dotnet.microsoft.com/)
[![.NET Aspire](https://img.shields.io/badge/.NET_Aspire-Orchestrated-blue?logo=dotnet)](https://learn.microsoft.com/dotnet/aspire/)
[![ICMR-NIN 2024](https://img.shields.io/badge/Clinical_Standards-ICMR--NIN_2024_%26_WHO-10b981)](https://www.nin.res.in/)
[![Design System](https://img.shields.io/badge/Aesthetic-Linear.app_Dark_Glassmorphism-6366f1)](https://linear.app)
[![License: AGPLv3 / SSPL v1](https://img.shields.io/badge/License-AGPLv3%20%2F%20SSPL%20v1-blue.svg)](LICENSE)
[![Security Policy](https://img.shields.io/badge/Security-Policy-success.svg)](SECURITY.md)

> **Diet Dost** (डाइट दोस्त / ડાયેટ દોસ્ત) is an enterprise-grade nutrition companion engineered specifically for the Indian population and South Asian metabolic phenotypes. It bridges clinical dietetics (ICMR-NIN 2024 and WHO guidelines) with modern multimodal AI meal vision (Microsoft Agent Framework powered by Google AI Gemini models).

---

## 🌟 Key Highlights

- **🏛️ .NET 11 Clean Architecture & Native CQRS**: Pure dependency-inversion Onion architecture with native CQRS (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`) implemented via `Microsoft.Extensions.DependencyInjection`—eliminating commercial licensing risks (Zero MediatR RPL-1.5). Thin controllers delegate exclusively to application handlers.
- **🔐 Swappable Database Secret Store**: Zero hardcoded secrets in source code or `appsettings.json`. Secrets are persisted in the `AppSecrets` database table via `ISecretStore` with in-memory caching and projected directly into ASP.NET Core `IConfiguration` via a custom `DatabaseConfigurationProvider` during host startup.
- **🛡️ Debug-Only Demo User Security Isolation**: Prevents production data breaches by restricting seeded demo accounts (`free@`, `basic@`, `premium@`, `admin.demo@`, `superadmin@dietdost.app`) strictly to Debug builds in Development hosting environments. In Release mode, demo user seeding is suppressed, login attempts are blocked with `HTTP 403 DemoAccessForbidden`, and any pre-existing demo accounts are proactively deactivated with revoked security stamps.
- **⚡ Dual SmartScheme Auth & Tier Quota Engine**: RFC 7519 JWT Bearer + HttpOnly Cookie dual authentication with India DPDPA 2023 forensic consent audit logging. Features dynamic tier quotas (`Free`: 1/day, `Basic`: 7/day, `Premium`: 30/day, `SuperAdmin`: Unlimited) with automated feature gating (visual progress comparisons and meal data exports).
- **🌐 Web BFF Composite Hydration**: High-performance Backend for Frontend (`/api/web/v1/dashboard`) dispatching CQRS queries concurrently via `Task.WhenAll` across isolated DI scopes for sub-50ms execution.
- **🧪 Mandatory End-to-End Tier Verification Harness**: 136 automated unit/eval tests plus an automated live E2E PowerShell test harness (`pwsh -File tests/validate_e2e_tiers.ps1`) verifying all 5 demo user tiers, quotas, feature gating, and admin permissions on the running WebGateway.
- **🇮🇳 South Asian & Indian Phenotype Specific**: Tailored for Indian dietary realities—including dal, sabzi, roti, rice, street snacks, and regional preparations—calibrated with WHO Asian-Indian BMI cutoffs (Normal: 18.5–22.9, Overweight: 23–24.9, Obese: $\ge$ 25 kg/m²).
- **🔬 Zero-Assumption Clinical Engine**: Zero hallucination or guesswork. Requires complete clinical profile metrics (age, biological sex, height, weight, activity multiplier, health conditions) before issuing caloric and macronutrient targets.
- **📸 Multimodal AI Meal Vision**: Upload or take photos of Indian dishes. Powered by Microsoft Agent Framework + Google Gemini multimodal vision with a strict $\ge 70\%$ confidence gating floor and editable 1-tap review modals.
- **🤖 Configurable Model Detection Transparency**: Real-time badge in the review modal displaying the active model that parsed the food (e.g. `gemini-3-flash-preview` or `gemini-2.5-flash`). Configurable via `"AI:ShowModelDetails": true`.
- **⚡ Gemini 3 Flash Thinking & Multi-Model Cascade**: Built-in resiliency for Gemini 3 Flash preview models with 8192 output token allocations and thinking tokens traversal, cascading seamlessly to Gemini 2.5 Flash and Gemini 2.5 Pro on quota or timeout.
- **🔍 End-to-End Aspire Observability & Tracing**: Complete HTTP request and response payload inspection in Aspire distributed traces (`HttpPayloadTelemetryMiddleware`), coupled with child `ai.food_detection` GenAI semantic spans (`gen_ai.system_prompt`, `user.diagnosed_conditions`, `user.medications`).
- **🔒 Resilient Database Engine & Non-PII Safeguards**: Schema-aware idempotent column migrations (`PRAGMA table_info`), EF Core collection `ValueComparer` instances (preventing change-tracking loss), and strict non-PII diagnostic error logging.
- **💡 Real-Time AI Macro Recalculation & Quantity Parsing**: Flexible detection of Indian cooking quantities (e.g. *"1.5 Cup"*, *"1 Katori"*, *"5-6 Slices"*, *"2 Phulkas"*). Dynamically recalculates total calories, protein, carbs, fat, fiber, and sugar whenever portions or ingredients are adjusted.
- **🥗 Complete Fiber & Sugar Nutrition Tracking**: Tracks dietary fiber (30g/day ICMR-NIN target) and free sugars (< 25g/day WHO threshold) across all meals, daily ledgers, and clinical targets.
- **📖 Logged Meals Diary & Excel Export**: Filter historical meals by graph section (1D, 7D, 30D, 90D, 365D), switch between interactive Card and Data Grid layouts, update or delete entries in place, and export complete nutritional history to Excel.
- **🗑️ Obsidian Dark Custom Delete Modal**: Replaces browser-native popups with a polished Linear.app modal featuring a pulsing danger icon, 6-macro mini-pills, and clinical deficit recalculation advisories.
- **🌍 User Profile Timezone & Universal UTC Storage**: Configurable user timezone with automatic browser detection (`Intl.DateTimeFormat`), EF Core universal UTC value converters, and circadian day-boundary meal groupings.
- **🖼️ Universal Obsidian Image Fallbacks**: Custom SVG vector graphics (`placeholder-meal.svg` and `placeholder-progress.svg`) with global capturing phase error interceptors ensuring zero broken image icons across all views.
- **🛡️ ICMR-NIN 2024 Clinical Safeguards**:
  - Enforced starvation caloric floors (1,200 kcal/day for females, 1,500 kcal/day for males).
  - Condition-specific metabolic adjustments (Hypothyroidism -12% TDEE, Diabetes low GI / low carb distribution, Hypertension 1.5g/day sodium ceiling, NAFLD saturated fat limits).
- **📸 Visual Transformation & Progress Tracking**: Face-fat reduction tracking, side-by-side baseline vs latest progress comparisons, and check-in timeline logging.
- **✨ Obsidian Linear-Class UI**: Ultra-refined dark glassmorphic design inspired by Linear.app with micro-animations, real-time HUD stats, and full calculation transparency sheets.

---

## 🏛️ Architecture & Tech Stack

Diet Dost is built on a decoupled **Clean Architecture & Native CQRS** pattern:

```mermaid
graph TB
%%
%% Copyright (c) 2026 diet-dost and/or its contributors.
%% Licensed under the "GNU Affero General Public License v3.0 only" and
%% the "Server Side Public License, v 1"; you may not use this file except
%% in compliance with, at your election, the "GNU Affero General Public
%% License v3.0 only" or the "Server Side Public License, v 1".
%%

    %% =========================================================================
    %% MASTER SOLUTION ARCHITECTURE: DIET DOST (.NET 11 & ASPIRE)
    %% Clean Architecture, Native CQRS, Azure Container Apps, SQLite SMB Mount, Key Vault
    %% =========================================================================

    subgraph LAYER_CICD ["0. CI/CD & PRE-DEPLOYMENT SECURITY GATE (Zero Deployment on Failure)"]
        direction TB
        GATE_Scanners["Security & Code Scanning Pipeline (security-scan.yml)<br/>Gitleaks | ESLint | Roslyn, DevSkim & CodeQL (All 7 .csproj) | Trivy | Checkov | actionlint | AI Defense"]
        GATE_Summary["Security Gate Summary Table & PR Comment Publisher<br/>Blocks Azure Deployment if Any Scanner Fails"]
        GATE_Deploy["Two-Stage Automated Deployment Workflows<br/>Stage 1: azure-infra-deploy.yml (Bicep IaC) | Stage 2: azure-app-deploy.yml (ACA & Custom Domain TLS)"]
        GATE_Scanners --> GATE_Summary
        GATE_Summary -->|100% Green PASS| GATE_Deploy
    end

    subgraph LAYER_DESIGN ["1. PRESENTATION LAYER (Linear.app Glassmorphic PWA Client)"]
        direction TB
        UI_Linear["Linear Design System<br/>(Obsidian #08090a, Linear Violet #5e6ad2, Emerald #27c380)<br/>Geist Sans & Tabular Numbers"]
        UI_AuthGate["Obsidian Dark Auth Gate<br/>(Dual DPDPA 2023 Forensic Consent & Tier Engine)"]
        UI_PWA["PWA Web Client & Mobile Ergonomics<br/>(Camera Capture, Habit Streak HUD, Macro Gauges, Mobile Bottom Nav)"]
        UI_Tiers["Dynamic Tier Status Badges<br/>(Free, Basic, ⚡ Premium, 👑 SuperAdmin)"]
        UI_Diary["Food Diary & History<br/>(1D/7D/30D/90D/365D Filter, Card/Grid Toggle, Excel Export)"]
        UI_DeleteModal["Custom Obsidian Delete Modal<br/>(Danger Pulse & Deficit Recalculation Advisory)"]
        UI_Fallback["Static Vector Fallback Armor<br/>(placeholder-meal.svg & placeholder-progress.svg)"]
        UI_Offline["Client-Side Offline Engine<br/>(WASM SQLite with OPFS / IndexedDB Dexie.js & ServiceWorker)"]
        
        UI_Linear --- UI_AuthGate
        UI_AuthGate --- UI_PWA
        UI_PWA --- UI_Tiers
        UI_PWA --- UI_Diary
        UI_Diary --- UI_DeleteModal
        UI_PWA --- UI_Fallback
        UI_PWA <-->|Offline Caching & Background Sync| UI_Offline
    end

    subgraph LAYER_SECURITY ["2. SECURITY & BOUNDARY DEFENSE LAYER (OWASP ASVS & LLM Defenses)"]
        direction TB
        SEC_Perimeter["Perimeter & Transport Security<br/>(TLS 1.3, Strict CSP, Minimal CORS, Secure HTTPOnly Cookies)"]
        SEC_DualAuth["Dual SmartScheme Authentication<br/>(RFC 7519 JWT Bearer + Secure HttpOnly Cookie)"]
        SEC_DebugGuard["Debug-Only Demo User Security Isolation<br/>(#if DEBUG & IAppEnvironment.AllowsDemoUsers)<br/>Release Mode Seeding Suppression & Auto-Deactivation"]
        SEC_RateLimit["ASP.NET Core RateLimiter<br/>(Polly Sliding Window per User IP / Bearer Token)"]
        SEC_FileArmor["File Ingestion Armor<br/>(Magic Byte Check: JPEG/PNG/WEBP, Max 8MB, EXIF GPS Stripper)"]
        SEC_AIGuard["PromptShieldValidator & Content Safety<br/>(Harmful, Violent, Sexual, Communal & Self-Learning Injection Shields)"]
        SEC_GoogleSafety["Google AI StrictSafetySettings<br/>(BLOCK_LOW_AND_ABOVE Harassment, Hate Speech, Sex, Dangerous)"]
        SEC_DataFilter["Data Isolation Guardrails<br/>(EF Core Global Query Filters: UserId == CurrentUser.Id)"]
        SEC_VaultAuth["Sole Authority Secret Governance (@nikunjbanker)<br/>Azure Key Vault with Purge Protection & Passwordless Managed Identity"]
    end

    subgraph LAYER_GATEWAY ["3. PRESENTATION GATEWAY & BACKEND-FOR-FRONTEND (Nutrition.WebGateway)"]
        direction TB
        CONTROLLERS["Thin REST Controllers (.NET 11)<br/>(AuthController, MealsController, ProfileController, AnalyticsController, AdminController)"]
        BFF_Composite["Web BFF Composite Hydration (/api/web/v1/dashboard)<br/>Parallel Task.WhenAll CQRS Execution & Sub-50ms Payload"]
        BFF_Mobile["Mobile BFF Facade (/api/mobile/v1/*)<br/>Compact Cellular DTOs & Hardware Token Handshake"]
        MW_Pipeline["HTTP Middleware Pipeline<br/>(Authentication, Rate Limiting, Exception Handling RFC 7807, HttpPayloadTelemetry)"]
        CONFIG_DB["Configuration Architecture<br/>(Strongly-Typed Options Pattern + FluentValidation + Azure Key Vault Provider)"]
    end

    subgraph LAYER_APPLICATION ["4. APPLICATION LAYER (Nutrition.Application - Clean Architecture)"]
        direction TB
        CQRS_Engine["Native CQRS Pipeline (Zero MediatR Dependency)<br/>(Pure Microsoft.Extensions.DependencyInjection Handlers)"]
        
        subgraph USECASES_AUTH ["Identity & Security Use Cases"]
            CMD_Login["LoginCommandHandler<br/>(Debug-Only Demo Guard & JWT Generation)"]
            CMD_Register["RegisterUserCommandHandler<br/>(DPDPA Consent & PasswordPolicy)"]
            CMD_Reset["ResetPasswordCommandHandler<br/>(Active User Check & Policy)"]
        end

        subgraph USECASES_VISION ["Meal Vision Use Cases"]
            CMD_MealUpload["UploadMealCommandHandler<br/>(Tier Quota Check & Cascade Vision)"]
            CMD_MealReview["ReviewMealCommandHandler<br/>(Macro Recalculation)"]
        end

        subgraph USECASES_PROFILE ["Clinical Profile Use Cases"]
            CMD_SaveProfile["SaveProfileCommandHandler<br/>(Mifflin-St Jeor & ICMR-NIN Safeguards)"]
            QRY_GetProfile["GetProfileQueryHandler<br/>(TDEE & Target Budget Calculation)"]
        end

        subgraph USECASES_ANALYTICS ["Analytics & Ledger Use Cases"]
            QRY_Ledger["GetTodayLedgerQueryHandler<br/>(6-Macro Balances & Daily Deficit)"]
            QRY_History["GetMealHistoryQueryHandler<br/>(Multi-Period Trend Filtering)"]
        end

        PORTS["Application Ports & Abstractions<br/>(ISecretStore, IAppEnvironment, IPhotoStorageService, IRepository, IUnitOfWork)"]
    end

    subgraph LAYER_DOMAIN ["5. DOMAIN CORE LAYER (Nutrition.Domain - DDD)"]
        direction TB
        subgraph AGGREGATES ["Domain Aggregates & Entities"]
            AGG_User["ApplicationUser (Aggregate Root)<br/>(DPDPA Consent Timestamps, SecurityStamp, DemoEmails, Tier, Role)"]
            AGG_Profile["UserProfile (Aggregate Root)<br/>(Height, Weight, Pace, Dietary Preference, IANA Timezone)"]
            AGG_Meal["MealLog (Aggregate Root)<br/>(MealType, PhotoUri, Status: Uploaded->Analyzed->Verified)"]
            AGG_Ledger["DailyCalorieLedger (Aggregate Root)<br/>(Date, Consumed, Budget, Pending Deficit)"]
            ENT_Secret["AppSecret Entity<br/>(Key, Value, Description, CreatedAtUtc, UpdatedAtUtc)"]
            ENT_TierConfig["TierFeatureConfiguration<br/>(DailyAiLimit: 1, 7, 30, -1; PhotoCompare; DataExport)"]
        end

        subgraph CLINICAL ["Clinical Dietetics Safeguards (ICMR-NIN 2024 & WHO)"]
            CALC_BMR["Mifflin-St Jeor BMR & TDEE Multipliers"]
            CLIN_Floors["Starvation Floors (1200 kcal F / 1500 kcal M)"]
            CLIN_Rules["Clinical Matrix (Diabetes, HTN, Thyroid, NAFLD Adjustments)"]
            CLIN_WHO["WHO Asian-Indian Cutoffs & 3:1 Cereal:Pulse Ratio"]
        end
    end

    subgraph LAYER_INFRASTRUCTURE ["6. INFRASTRUCTURE & ZERO-TRUST CLOUD (Nutrition.Infrastructure & Azure)"]
        direction TB
        ADAPTER_Secrets["KeyVault & DatabaseSecretStore Adapters<br/>(ConcurrentDictionary In-Memory Cache)"]
        ADAPTER_Env["AppEnvironment Adapter<br/>(#if DEBUG Preprocessor & IHostEnvironment)"]
        ADAPTER_Repo["EfRepository & EfUnitOfWork<br/>(Generic EF Core Data Access)"]
        ADAPTER_Photo["LocalPhotoStorageService<br/>(Cryptographic SHA-256 Hashed File Storage)"]
        ADAPTER_Jwt["JwtTokenService<br/>(HMAC-SHA256 Token Issuer via ISecretStore)"]
        ADAPTER_Agent["Microsoft Agent Framework Vision Agent<br/>(Multi-Model Cascade: 3-Flash -> 2.5-Flash -> 2.5-Pro)"]
        
        DB_Context["DietTrackerDbContext<br/>(Universal UTC ValueConverter, Schema-Aware PRAGMA, Collection ValueComparers)"]
        AZURE_NSG["Network Security Group: nsg-dietdost-dev<br/>Stateful Zero-Trust Firewall (Checkov CKV_AZURE_9 & CKV_AZURE_160)<br/>HTTPS Inbound 443 | Port 445 Storage | 443 Cloud & Gemini AI"]
        AZURE_ACA["Azure Container Apps (Serverless MicroVM)<br/>Single Replica Invariant (min:1, max:1)<br/>Custom Domain: dev.dietdost.app with Free Managed TLS"]
        AZURE_SMB[("Azure Files Persistent SMB Share<br/>Mount: /app/data/diet_dost.db<br/>SMB 3.1.1 AES-128/256-GCM Wire Encryption<br/>7-Day Soft-Delete Protection (Zero Data Loss)")]
        AZURE_AKV[("Azure Key Vault (Purge Protection Enabled)<br/>Sole Authority: @nikunjbanker<br/>Managed Identity Access & AuditEvent Diagnostics")]
        AZURE_ACR[("Azure Container Registry (ACR)<br/>Passwordless Image Pull (acrPullRole)")]
        AZURE_ALERT["Azure Monitor Threat Metric Alert<br/>Key Vault 401/403 Unauthorized Spikes (>5 in 5m)"]
    end

    subgraph LAYER_ORCHESTRATION ["7. DEVOPS, GOVERNANCE & OBSERVABILITY (.NET Aspire 13.5.4)"]
        direction TB
        ASPIRE_Host[".NET Aspire AppHost (net11.0 / Sdk 13.5.4)<br/>(Distributed Orchestration & Typed Topology)"]
        ASPIRE_Dash["Aspire Developer Dashboard (:18888)<br/>(Live Resources, Distributed Traces, GenAI Semantic Spans)"]
        OTEL_Collector["OpenTelemetry (OTel) Pipeline<br/>(NutritionTelemetry ActivitySource 'Nutrition.DietDost')"]
        TEST_Harness["Validation Harnesses<br/>(242 Automated Tests across 7 Projects + validate_e2e_tiers.ps1 Across 5 Demo User Tiers)"]
        ADR_Registry["Subsystem Domain ADR Architecture (docs/adr/)<br/>(architecture, security, devops, presentation, governance)<br/>docs/adr/index.json Machine-Readable Registry (~4 KB)"]
    end

    %% Flow Connections
    GATE_Deploy -.->|Deploys Infrastructure & Container| AZURE_ACA
    UI_PWA -->|HTTPS / WSS| SEC_Perimeter
    SEC_Perimeter --> SEC_RateLimit
    SEC_RateLimit --> SEC_DualAuth
    SEC_DualAuth --> CONTROLLERS
    
    CONTROLLERS --> MW_Pipeline
    CONTROLLERS --> CQRS_Engine
    CONFIG_DB -.->|Injects Options & Secrets| CONTROLLERS
    
    CQRS_Engine --> CMD_Login
    CQRS_Engine --> CMD_Register
    CQRS_Engine --> CMD_MealUpload
    CQRS_Engine --> CMD_SaveProfile
    CQRS_Engine --> QRY_Ledger

    CMD_Login --> SEC_DebugGuard
    CMD_Login --> PORTS
    CMD_MealUpload --> SEC_AIGuard
    SEC_AIGuard --> SEC_GoogleSafety
    CMD_MealUpload --> PORTS
    CMD_SaveProfile --> PORTS
    QRY_Ledger --> PORTS

    PORTS -.->|Implements| ADAPTER_Secrets
    PORTS -.->|Implements| ADAPTER_Env
    PORTS -.->|Implements| ADAPTER_Repo
    PORTS -.->|Implements| ADAPTER_Photo

    ADAPTER_Repo --> DB_Context
    ADAPTER_Secrets --> DB_Context
    DB_Context --> AZURE_SMB
    ADAPTER_Secrets -.->|Runtime Secrets| AZURE_AKV
    AZURE_NSG --- AZURE_ACA
    AZURE_ACA -->|Volume Mount (SMB 3.1.1)| AZURE_SMB
    AZURE_ACA -.->|Pulls Image| AZURE_ACR
    AZURE_AKV -.->|Diagnostic Audit Logs| OTEL_Collector
    AZURE_AKV -.->|Monitored By| AZURE_ALERT
    ADAPTER_Agent --> CLOUD_AI["Google Gemini Multimodal Vision API"]

    CMD_SaveProfile --> CLINICAL
    CLINICAL --> AGG_Profile
    CMD_Login --> AGG_User

    %% Telemetry & Observability
    MW_Pipeline -.->|Payload Telemetry| OTEL_Collector
    CQRS_Engine -.->|GenAI Semantic Spans| OTEL_Collector
    OTEL_Collector --> ASPIRE_Dash
    ASPIRE_Host -->|Orchestrates| CONTROLLERS
    ASPIRE_Host -->|Orchestrates| ASPIRE_Dash
    TEST_Harness -.->|Validates Solution| CONTROLLERS
```

### Technology Matrix

| Layer | Technologies |
|---|---|
| **Platform** | [.NET 11 RC](https://dotnet.microsoft.com/) (`net11.0`) / C# 13 |
| **Architecture** | Clean Architecture (Onion/Hexagonal), Native CQRS via `Microsoft.Extensions.DependencyInjection` (Zero MediatR) |
| **Orchestration & Dashboard** | [.NET Aspire 13.5.4](https://learn.microsoft.com/dotnet/aspire/) (Standalone AppHost SDK) |
| **Secrets Management** | Database Secret Store (`AppSecrets` table, `ISecretStore` port, `DatabaseConfigurationProvider`) |
| **Security & Environment** | `IAppEnvironment` dual-guard (#if DEBUG & `IHostEnvironment`), Dual SmartScheme JWT Bearer + HttpOnly Cookies |
| **Observability** | OpenTelemetry, `HttpPayloadTelemetryMiddleware`, GenAI Semantic Conventions |
| **Domain Logic** | Domain-Driven Design (DDD), ICMR-NIN 2024, WHO Asian-Indian Guidelines, Mifflin-St Jeor Equation |
| **AI / Multimodal Vision** | Microsoft Agent Framework + Google Gemini AI (3-Flash, 2.5-Flash, 2.5-Pro Cascade) |
| **Frontend** | Vanilla ES Modules, CSS Glassmorphism (Linear.app aesthetic), Chart.js, HTML5 Canvas, PWA |
| **Database** | SQLite V1 (Universal UTC `ValueConverter`, Schema-aware `PRAGMA table_info` checks, EF Core collection `ValueComparer`s) |

---

## 📁 Repository Structure

```
diet-dost/
├── src/
│   ├── Nutrition.AppHost/           # Standalone .NET Aspire 13.5.4 AppHost orchestration topology
│   ├── Nutrition.Domain/            # DDD core entities, clinical calculators, AppSecret, password policy
│   ├── Nutrition.Application/       # Native CQRS commands, queries, handlers, ISecretStore, IAppEnvironment
│   ├── Nutrition.Infrastructure/    # DatabaseSecretStore, DatabaseConfigurationProvider, EF Core DbContext, AI Agent
│   └── Nutrition.WebGateway/        # ASP.NET Core WebGateway, thin controllers, SmartScheme auth, PWA static files
│       └── wwwroot/
│           ├── assets/              # SVG vectors (placeholder-meal.svg, placeholder-progress.svg)
│           ├── css/                 # Linear.app glassmorphic stylesheets
│           ├── js/                  # ES Module client (di, services, state, ui)
│           ├── partials/            # 11 modular HTML components (auth-gate, review-modal, delete-meal-modal, HUD, etc.)
│           └── index.html           # Single Page App shell
├── tests/
│   ├── Nutrition.Domain.Tests/      # Unit tests for clinical formulas & medical safeguards (36 tests)
│   ├── Nutrition.EvalHarness.Tests/ # AI Vision evals, auth, rate limiting, and secret store tests (100 tests)
│   └── validate_e2e_tiers.ps1       # Automated live end-to-end user tier validation test harness
├── docs/
│   ├── architecture/diagrams/       # Standalone synchronized Mermaid architecture diagrams
│   └── sdd/                         # Comprehensive Living Software Design Documents (SDD v1.3.1)
│       ├── 00_sdd_index.md          # Master index & traceability matrix
│       ├── 01_clinical_dietetics_spec.md
│       ├── 02_solution_architecture.md
│       ├── 03_data_models_and_contracts.md
│       ├── 04_security_and_compliance.md
│       ├── 05_devops_and_infrastructure.md
│       ├── 06_test_harness_and_evals.md
│       └── 07_living_documentation_log.md
├── LICENSE                          # MIT License
└── README.md                        # Project documentation
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2024 / 2026 or VS Code with C# Dev Kit
- Google Gemini API Key (for AI Multimodal Food Vision)

### 1. Clone the Repository

```bash
git clone https://github.com/nikunjbanker/diet-dost.git
cd diet-dost
```

### 2. Configure AI API Key & Model Troubleshooting Badge

Add your Gemini API Key in `src/Nutrition.WebGateway/appsettings.json` or use .NET User Secrets:

```json
{
  "AI": {
    "Provider": "GoogleAI",
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ModelId": "gemini-3-flash-preview",
    "FallbackModelId": "gemini-3.6-flash",
    "ShowModelDetails": true
  }
}
```

Or via .NET Secret Manager (recommended for local development):

```bash
cd src/Nutrition.WebGateway
dotnet user-secrets set "AI:ApiKey" "YOUR_API_KEY"
```

### 3. Build and Run

#### Using .NET CLI:
```bash
dotnet run --project src/Nutrition.WebGateway
```

Open your browser and navigate to:
```
http://localhost:5240
```

#### Using .NET Aspire:
```bash
dotnet run --project src/Nutrition.AppHost
```

- **Aspire Dashboard**: [`http://localhost:18888`](http://localhost:18888) (Inspect live resources, logs, traces with request/response bodies, and GenAI spans)
- **Application (Linear PWA)**: [`http://localhost:5240`](http://localhost:5240)

---

## 🧪 Running Tests & Validation

### 1. Automated Test Suite (136 Tests, 0 Warnings, 0 Errors)
Execute the comprehensive domain, clinical, security, and AI evaluation suite:

```bash
dotnet test
```

Test coverage includes:
- **Clinical & Safeguards (`Nutrition.Domain.Tests` - 36 tests)**:
  - Mifflin-St Jeor South Asian BMR/TDEE calculations.
  - WHO Asian-Indian BMI boundaries and cardiometabolic cutoffs.
  - ICMR-NIN 2024 starvation caloric floors (1,200 kcal F / 1,500 kcal M).
  - Health condition macro adjustments (Diabetes, HTN, Thyroid, NAFLD).
- **Security & Infrastructure (`Nutrition.EvalHarness.Tests` - 100 tests)**:
  - Multimodal AI food vision prompt defense, confidence gating ($\ge 70\%$), and fallback cascade.
  - PBKDF2 password hashing (HMAC-SHA512) and strict password policy validation.
  - RFC 7519 JWT Bearer authentication and HttpOnly session validation.
  - Polly sliding-window rate limiting resilience pipelines.
  - Database Secret Store (`AppSecrets` table, `ISecretStore` port, and caching).
  - Debug-only demo user isolation and release mode login rejection (`HTTP 403 DemoAccessForbidden`).

### 2. Live End-to-End User Tier Validation Harness
Run the automated end-to-end product verification across all 5 demo user tiers on the running WebGateway (`http://localhost:5240`):

```powershell
pwsh -File tests/validate_e2e_tiers.ps1
```

Validates real-time authentication, JWT issuance, profile calculations, daily AI quotas (`Free`: 1, `Basic`: 7, `Premium`: 30, `SuperAdmin`: Unlimited), feature gating (progress photos & CSV export paywalls), and Admin role authorization.

---

## 🚀 Cloud Deployment (Azure Container Apps + Persistent SQLite SMB)

Diet-Dost delivers a workable public cloud showcase MVP hosted on **Azure Container Apps (ACA)** at **`https://dev.diet-dost.in`** with zero external cloud SQL costs (<$0.30/month) adhering to the **SQLite Cloud Persistence Doctrine**:

### Architecture & Zero Data Loss Principles:
1. **Persistent SMB Mount**: Azure Files SMB 3.0 share (`dietdost-data`) mounted directly to `/app/data` for transactional durability.
2. **Single Replica Constraint**: `minReplicas: 1`, `maxReplicas: 1` enforced in Bicep to eliminate network file locking deadlocks.
3. **Private Network Perimeter**: Storage Account isolates traffic via dedicated VNet subnet and `Microsoft.Storage` service endpoint (`defaultAction: Deny`).
4. **Free Managed TLS 1.3**: Automatic 90-day auto-renewing certificate via Azure Container Apps managed environment.
5. **Showcase 5-Tier Governance**: Configured with `Security:AllowDemoUsers=true` enabling live validation across all 5 user tiers.

### On-Demand Deployment via GitHub Actions:
Deployments are managed exclusively via the automated GitHub Actions workflow ([`.github/workflows/azure-deploy.yml`](.github/workflows/azure-deploy.yml)) triggered on-demand (`workflow_dispatch`):

```bash
# Trigger via GitHub CLI:
gh workflow run "Build and Deploy to Azure Container Apps" -f allowDemoUsers=true
```
Or via GitHub Web UI: Navigate to **Actions** → **Build and Deploy to Azure Container Apps** → **Run workflow**.

### DNS Records for Custom Domain (`dev.diet-dost.in`):
| Type | Host | Target / Value | Purpose |
| :--- | :--- | :--- | :--- |
| **CNAME** | `dev` | `<app-fqdn>.azurecontainerapps.io` | Web traffic routing |
| **TXT** | `asuid.dev` | `<customDomainVerificationId>` | Azure domain ownership verification |

---

## 📚 Living Documentation (SDD v1.3.1)

Diet Dost strictly adheres to living documentation practices. Every architectural decision, clinical dietetic formula, and security standard is documented in detail:

- **[00: Master Index & Traceability (v1.3.1)](docs/sdd/00_sdd_index.md)**
- **[01: Clinical Dietetics Specification (v1.3.1)](docs/sdd/01_clinical_dietetics_spec.md)**
- **[02: Solution Architecture Blueprint (v1.3.1)](docs/sdd/02_solution_architecture.md)**
- **[03: Data Models & Contracts (v1.3.1)](docs/sdd/03_data_models_and_contracts.md)**
- **[04: Security & Compliance / OWASP (v1.3.1)](docs/sdd/04_security_and_compliance.md)**
- **[05: DevOps & Infrastructure (v1.3.1)](docs/sdd/05_devops_and_infrastructure.md)**
- **[06: Test Harness & AI Vision Evals (v1.3.1)](docs/sdd/06_test_harness_and_evals.md)**
- **[07: Living Documentation & Audit Log (Synchronized)](docs/sdd/07_living_documentation_log.md)**
- **[Local Indian Food Text/Vision SLM Training Guide](docs/LOCAL_INDIAN_FOOD_SLM_TRAINING_GUIDE.md)** — dataset design, local QLoRA training, Microsoft Agent Framework + Ollama integration, and evaluation gates

---

## 📄 License & Governance

This project is dual-licensed under the **GNU Affero General Public License v3.0 only (AGPLv3)** and the **Server Side Public License, v 1 (SSPL)**. See [LICENSE](LICENSE) and [CONTRIBUTING.md](CONTRIBUTING.md) for full terms. License governance and automated header verification are maintained via `.agents/skills/diet-dost-license-governance/`.

For vulnerability reporting procedures and comprehensive security architecture standards, please review our repository [SECURITY.md](SECURITY.md).

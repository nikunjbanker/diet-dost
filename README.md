# Diet Dost 🥗 | AI-Powered Indian Diet & Calorie Tracker

[![.NET 11](https://img.shields.io/badge/.NET-11%20RC-512bd4?logo=dotnet)](https://dotnet.microsoft.com/)
[![.NET Aspire](https://img.shields.io/badge/.NET_Aspire-Orchestrated-blue?logo=dotnet)](https://learn.microsoft.com/dotnet/aspire/)
[![ICMR-NIN 2024](https://img.shields.io/badge/Clinical_Standards-ICMR--NIN_2024_%26_WHO-10b981)](https://www.nin.res.in/)
[![Design System](https://img.shields.io/badge/Aesthetic-Linear.app_Dark_Glassmorphism-6366f1)](https://linear.app)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Diet Dost** (डाइट दोस्त / ડાયેટ દોસ્ત) is an enterprise-grade nutrition companion engineered specifically for the Indian population and South Asian metabolic phenotypes. It bridges clinical dietetics (ICMR-NIN 2024 and WHO guidelines) with modern multimodal AI meal vision (Microsoft Agent Framework powered by Google AI Gemini models).

---

## 🌟 Key Highlights

- **🇮🇳 South Asian & Indian Phenotype Specific**: Tailored for Indian dietary realities—including dal, sabzi, roti, rice, street snacks, and regional preparations—calibrated with WHO Asian-Indian BMI cutoffs (Normal: 18.5–22.9, Overweight: 23–24.9, Obese: $\ge$ 25 kg/m²).
- **🔬 Zero-Assumption Clinical Engine**: Zero hallucination or guesswork. Requires complete clinical profile metrics (age, biological sex, height, weight, activity multiplier, health conditions) before issuing caloric and macronutrient targets.
- **📸 Multimodal AI Meal Vision**: Upload or take photos of Indian dishes. Powered by Microsoft Agent Framework + Google Gemini multimodal vision with a strict $\ge 70\%$ confidence gating floor and editable 1-tap review modals.
- **🤖 Configurable Model Detection Transparency**: Real-time badge in the review modal displaying the active model that parsed the food (e.g. `gemini-3-flash-preview` or `gemini-2.5-flash`). Configurable via `"AI:ShowModelDetails": true`.
- **⚡ Gemini 3 Flash Thinking & Multi-Model Cascade**: Built-in resiliency for Gemini 3 Flash preview models with 8192 output token allocations and thinking tokens traversal, cascading seamlessly to Gemini 2.5 Flash and Gemini 2.5 Pro on quota or timeout.
- **🔍 End-to-End Aspire Observability & Tracing**: Complete HTTP request and response payload inspection in Aspire distributed traces (`HttpPayloadTelemetryMiddleware`), coupled with child `ai.food_detection` GenAI semantic spans (`gen_ai.system_prompt`, `user.diagnosed_conditions`, `user.medications`).
- **🔒 Resilient Database Engine & Non-PII Safeguards**: Schema-aware idempotent column migrations (`PRAGMA table_info`), EF Core collection `ValueComparer` instances (preventing change-tracking loss), and strict non-PII diagnostic error logging.
- **💡 Real-Time AI Macro Recalculation**: Change food items, serving quantities, or oil levels on the fly. The intelligent client/gateway recalculates calories, protein, carbs, fats, fiber, and sodium in real-time.
- **🛡️ ICMR-NIN 2024 Clinical Safeguards**:
  - Enforced starvation caloric floors (1,200 kcal/day for females, 1,500 kcal/day for males).
  - Condition-specific metabolic adjustments (Hypothyroidism -12% TDEE, Diabetes low GI / low carb distribution, Hypertension 2g/day sodium ceiling, NAFLD saturated fat limits).
- **📸 Visual Transformation & Progress Tracking**: Face-fat reduction tracking, side-by-side baseline vs latest progress comparisons, and check-in timeline logging.
- **✨ Obsidian Linear-Class UI**: Ultra-refined dark glassmorphic design inspired by Linear.app with micro-animations, real-time HUD stats, and full calculation transparency sheets.

---

## 🏛️ Architecture & Tech Stack

Diet Dost is built on a clean **Domain-Driven Design (DDD)** onion architecture:

```mermaid
graph TD
    subgraph Client ["Frontend (PWA)"]
        UI["Linear Obsidian Web Client (HTML5 / Vanilla ES Modules)"]
        DI["Client Dependency Injection & EventBus"]
        Partials["Modular HTML Partials (HUD, Camera, Modals, History)"]
        Badge["Model Transparency Badge (Configurable)"]
    end

    subgraph Gateway ["Web & API Gateway"]
        WebGateway["Nutrition.WebGateway (.NET 11 RC ASP.NET Core)"]
        MW["HttpPayloadTelemetryMiddleware (Trace Payloads)"]
        StaticFileServer["Static File & Partial Loader Engine"]
    end

    subgraph Core ["Application & Domain"]
        Domain["Nutrition.Domain (DDD Core)"]
        Clinical["Clinical Calculations & ICMR-NIN Safeguards"]
        Contracts["Data Contracts & Aggregates"]
        Telemetry["NutritionTelemetry (GenAI Semantic ActivitySource)"]
    end

    subgraph Infra ["Infrastructure & Persistence"]
        SQLite["Swappable SQLite Persistence (PRAGMA Checks & ValueComparers)"]
        AgentVision["Microsoft Agent Framework Vision Agent"]
        Cascade["Multi-Model Fallback Cascade (3-Flash -> 2.5-Flash -> 2.5-Pro)"]
        Aspire["Aspire Orchestrator & Dashboard (Blazor JS Patched)"]
    end

    UI --> WebGateway
    WebGateway --> MW
    WebGateway --> Domain
    WebGateway --> SQLite
    WebGateway --> AgentVision
    AgentVision --> Cascade
    Cascade --> GoogleGemini["Google Gemini AI Vision API"]
    Domain --> Telemetry
    Telemetry -.-> Aspire
    MW -.-> Aspire
```

### Technology Matrix

| Layer | Technologies |
|---|---|
| **Platform** | [.NET 11 RC](https://dotnet.microsoft.com/) / C# 13 |
| **Orchestration & Dashboard** | [.NET Aspire 13.5.4](https://learn.microsoft.com/dotnet/aspire/) (Blazor Virtualize JS Patched) |
| **Observability** | OpenTelemetry, `HttpPayloadTelemetryMiddleware`, GenAI Semantic Conventions |
| **Domain Logic** | Clean Architecture / Domain-Driven Design (DDD) |
| **AI / Multimodal Vision** | Microsoft Agent Framework + Google Gemini AI (3-Flash, 2.5-Flash, 2.5-Pro Cascade) |
| **Frontend** | Vanilla ES Modules, CSS Glassmorphism, Chart.js, HTML5 Canvas, PWA |
| **Database** | SQLite V1 (Schema-aware `PRAGMA table_info` checks, EF Core collection `ValueComparer`s) |
| **Clinical Guidelines** | ICMR-NIN 2024, WHO Asian-Indian Guidelines, Mifflin-St Jeor Equation |

---

## 📁 Repository Structure

```
diet-dost/
├── src/
│   ├── Nutrition.AppHost/           # .NET Aspire orchestration host & typed resource topology
│   ├── Nutrition.ServiceDefaults/   # Resilience, OpenTelemetry, health checks
│   ├── Nutrition.Domain/            # DDD entities, clinical calculators, aggregates
│   ├── Nutrition.Application/       # NutritionTelemetry ActivitySource & application services
│   ├── Nutrition.Infrastructure/    # AI Vision agent, EF Core DbContext, repositories, ValueComparers
│   └── Nutrition.WebGateway/        # ASP.NET Core gateway, HttpPayloadTelemetryMiddleware, PWA
│       └── wwwroot/
│           ├── css/                 # Linear.app glassmorphic stylesheets
│           ├── js/                  # ES Module client (di, services, state, ui)
│           ├── partials/            # 10 modular HTML UI components (review-modal, HUD, etc.)
│           └── index.html           # Single Page App shell
├── tests/
│   ├── Nutrition.Domain.Tests/      # Unit tests for clinical formulas & safeguards
│   └── Nutrition.Vision.Evals/      # AI food vision evaluation harness & benchmark tests
├── docs/
│   ├── architecture/diagrams/       # Standalone synchronized Mermaid architecture diagrams
│   └── sdd/                         # Comprehensive Software Design Documents (SDD v1.2.0)
│       ├── 00_sdd_index.md          # Master index & traceability matrix (v1.2.0)
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

- [.NET 11 SDK (or .NET 9+)](https://dotnet.microsoft.com/download)
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
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-3-flash-preview",
    "FallbackModels": ["gemini-2.5-flash", "gemini-2.5-pro"]
  },
  "AI": {
    "ShowModelDetails": true
  }
}
```

Or via .NET Secret Manager (recommended for local development):

```bash
cd src/Nutrition.WebGateway
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY"
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

## 🧪 Running Tests

Execute the automated clinical unit tests and evaluation harnesses:

```bash
dotnet test
```

Test coverage includes:
- Mifflin-St Jeor basal metabolic rate calculations.
- WHO South Asian BMI classification boundaries.
- ICMR-NIN 2024 starvation floor compliance (male & female).
- Health condition adjustments (Hypothyroidism, Hypertension, Diabetes, NAFLD).
- Food vision prompt evaluation gating and multi-model fallback.
- Database schema migration idempotency and collection value comparers.

---

## 📚 Living Documentation (SDD)

Diet Dost strictly adheres to living documentation practices. Every architectural decision, clinical dietetic formula, and security standard is documented in detail:

- **[00: Master Index & Traceability (v1.2.0)](docs/sdd/00_sdd_index.md)**
- **[01: Clinical Dietetics Specification (v1.2.0)](docs/sdd/01_clinical_dietetics_spec.md)**
- **[02: Solution Architecture Blueprint (v1.2.0)](docs/sdd/02_solution_architecture.md)**
- **[03: Data Models & Contracts (v1.2.0)](docs/sdd/03_data_models_and_contracts.md)**
- **[04: Security & Compliance / OWASP (v1.2.0)](docs/sdd/04_security_and_compliance.md)**
- **[05: DevOps & Infrastructure (v1.2.0)](docs/sdd/05_devops_and_infrastructure.md)**
- **[06: Test Harness & AI Vision Evals (v1.2.0)](docs/sdd/06_test_harness_and_evals.md)**
- **[07: Living Documentation & Audit Log (Synchronized)](docs/sdd/07_living_documentation_log.md)**

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

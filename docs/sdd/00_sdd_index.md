# SDD Master Index, Roadmap & Traceability Matrix
> **Specification Version**: `v1.3.1 (Production & Living SDD)`  
> **Classification**: Master Software Design Document (SDD) Index  
> **Approved Domain Focus**: Indian Population, ICMR-NIN 2024 & WHO Medical Standards  
> **Tech Stack**: .NET 11 RC, .NET Aspire, Swappable SQLite V1 (PWA Offline-First), Microsoft Agent Framework + Google AI Pro, OWASP ASVS, Linear.app Design System  

---

## 1. Executive Architecture Summary

**Diet Dost** is an enterprise-grade, AI-powered nutrition companion engineered specifically for the Indian population and South Asian metabolic phenotypes. It bridges clinical dietetics (ICMR-NIN 2024 and WHO guidelines) with modern AI multimodal meal vision (Microsoft Agent Framework powered by Google AI Gemini models).

The solution adheres to Domain-Driven Design (DDD) bounded contexts, zero-assumption intake guarantees, OWASP ASVS Level 2 perimeter security, swappable SQLite/PostgreSQL persistence, and offline-first Progressive Web App (PWA) capabilities.

---

## 2. Document Inventory

| Document Ref | Document Title | Description | Status |
|---|---|---|---|
| [`00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md) | **Master Index & Traceability** | Executive summary, document inventory, progress matrix, and traceability | `APPROVED` |
| [`01_clinical_dietetics_spec.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/01_clinical_dietetics_spec.md) | **Clinical Dietetics Specification** | ICMR-NIN 2024 & WHO rules, Mifflin-St Jeor math, clinical adjustments matrix | `APPROVED` |
| [`02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md) | **Solution Architecture Blueprint** | Master multi-dimensional diagrams (Design, Security, App, DevOps, Functional) | `APPROVED` |
| [`03_data_models_and_contracts.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_data_models_and_contracts.md) | **Data Models & Contracts** | DDD aggregates, value objects, EF Core schema, JSON contract schema | `APPROVED` |
| [`04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md) | **Security & Compliance (OWASP)** | OWASP ASVS blueprint, magic-byte validation, prompt guardrails, rate limits | `APPROVED` |
| [`05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md) | **DevOps & Infrastructure** | .NET Aspire 11 AppHost topology, OTel pipelines, Redis caching, Docker runbook | `APPROVED` |
| [`06_test_harness_and_evals.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/06_test_harness_and_evals.md) | **Test Harnesses & Vision Evals** | Closed-loop testing, Aspire test harness, AI vision benchmarks, clinical unit tests | `APPROVED` |
| [`07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md) | **Living Documentation Log** | Continuous chronological audit trail of features, defect fixes, and RCAs | `SYNCHRONIZED` |
| [`08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md) | **Cross-Platform (Web & Mobile) Clean Architecture & Dual BFF Plan** | Unified Web & Mobile architecture review, Dual BFF design, zero duplicate clinical code, and phased Stacked PR tracks | `APPROVED` |
| [`../cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md) | **CFT: Web BFF & Clean Architecture Verification** | Functional acceptance checklist for Web single-roundtrip hydration, zero client math, and modular UI controls | `ACTIVE CFT` |
| [`../cft/cft_mobile_mvp_cross_platform.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_mobile_mvp_cross_platform.md) | **CFT: Mobile MVP Cross-Platform (Android & iOS)** | Functional acceptance checklist for Mobile camera, 1080p SkiaSharp compression, hardware token security, and HUD | `ACTIVE CFT` |
| [`../cft/cft_cross_platform_functional_parity_matrix.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_cross_platform_functional_parity_matrix.md) | **CFT: Cross-Platform Functional Parity Matrix** | Master parity scorecard cross-checking Web vs Android vs iOS across all features, tiers, and clinical invariants | `ACTIVE CFT` |
| [`USER_MANAGEMENT_AND_SECURITY_ARCHITECTURE_PLAN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/USER_MANAGEMENT_AND_SECURITY_ARCHITECTURE_PLAN.md) | **User Management & Security Architecture Plan** | Complete implementation blueprint for Auth, Tiers, DPDPA dual-consent, and dynamic AI quotas | `APPROVED` |
| [`ICMR_NIN_2024_FEATURE_ROADMAP.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/ICMR_NIN_2024_FEATURE_ROADMAP.md) | **ICMR-NIN 2024 Feature Roadmap** | Comprehensive 17-guideline feature recommendations, prioritization matrix, and roadmap | `PROPOSED & AUDITED` |
| [`LOCAL_INDIAN_FOOD_SLM_TRAINING_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/LOCAL_INDIAN_FOOD_SLM_TRAINING_GUIDE.md) | **Local Indian Food Text/Vision SLM Training Guide** | Dataset, local fine-tuning, Agent Framework integration, and evaluation gates | `PROPOSED` |
| [`AZURE_DEVOPS_DEPLOYMENT_TODO.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/AZURE_DEVOPS_DEPLOYMENT_TODO.md) | **Azure DevOps & Deployment Feature TODO** | Podman 5.7.0, Azure Container Apps, ACR push, custom domain & free managed TLS | `APPROVED & ACTIONABLE` |


---

## 3. Implementation Progress Matrix

| Layer / Component | Specification Section | Implementation Status | Test Coverage |
|---|---|---|---|
| **Zero-Assumption Clinical Engine** | Skill §1.1 – §1.5 | Complete (`ClinicalCalculators.cs`, `ClinicalDietitianService.cs`) | 100% Passed (Unit Tests) |
| **Mifflin-St Jeor & TDEE Math** | Skill §1.2 | Complete (Men/Women baselines, activity multipliers) | 100% Passed (Unit Tests) |
| **Clinical Adjustment Matrix** | Skill §1.5 | Complete (Diabetes, HTN, Thyroid, Lipids, PCOS, Gout, NAFLD) | 100% Passed (Unit Tests) |
| **AI Multimodal Vision Agent** | Skill §2.1 – §2.4 | Complete (`MicrosoftAgentFoodVisionService.cs`, Confidence Gating $\ge 70\%$) | 100% Passed (Eval Harness) |
| **Gemini 3 Flash Thinking & Fallback** | Skill §2.2 | Complete (8192 `max_output_tokens`, thought-parts traversal, fallback cascade) | Verified in Live Evals |
| **Configurable Model Transparency Badge** | Skill §2.3, §5.1 | Complete (`"AI:ShowModelDetails": true`, `review-modal.html`, `review-modal.js`) | Verified via Browser Subagent |
| **Quantity Detection & Editable Portions** | Skill §2.3 | Complete (`Quantity` detection, unit steppers, real-time macro recalculation) | Verified in Browser |
| **Fiber & Sugar Nutrition Tracking** | Skill §1.1, §1.3 | Complete (`FiberGrams`, `SugarGrams`, WHO free sugar ceiling < 25g/day) | 100% Passed (Unit Tests) |
| **Food Diary & Excel Export** | Skill §5.1 | Complete (1D/7D/30D/90D/365D filter, Card/Grid layouts, XLSX/CSV export) | Verified in Browser |
| **Obsidian Dark Delete Confirmation Modal** | Skill §5.1 | Complete (Linear danger modal, 6-macro pills, deficit impact advisory) | Verified in Browser |
| **Profile Timezone & Universal UTC Persistence** | Skill §3.3, §3.4 | Complete (EF Core ValueConverter, `Timezone` column, circadian day grouping) | 100% Passed (Unit Tests) |
| **Universal Obsidian SVG Image Fallbacks** | Skill §3.5 | Complete (`placeholder-meal.svg`, `placeholder-progress.svg`, window error listener) | Verified via Browser Subagent |
| **Continuous Learning & Adaptive Memory**| Skill §2.4, §3.1 | Complete (`UserCorrectionRecord.cs`, `UserCorrectionRecordConfiguration.cs`) | Verified via Integration |
| **Swappable Persistence Engine** | Skill §3.1 – §3.2 | Complete (`StorageInfrastructureExtensions.cs`, SQLite V1) | 100% Passed (Integration) |
| **Schema-Safe DB Migrations & Comparers**| Skill §3.1, §4.2 | Complete (`PRAGMA table_info` checks, EF Core collection `ValueComparer`s) | 0 Warnings, 0 Runtime Errors |
| **Linear.app Design System PWA** | Skill §5.1 – §5.3 | Complete (Obsidian dark glassmorphism, HUD, Toast, Transparency) | Verified via Browser Subagent |
| **Modular ES Modules & Partials** | Skill §5.1 | Complete (11 HTML partials, DI container, EventBus, State store) | Verified via Browser Subagent |
| **Visual Transformation & Progress**| Skill §5.4 | Complete (Baseline vs Latest Face, Full Body, Check-In capture) | Verified via Browser Subagent |
| **DevOps & Aspire AppHost Topology**| Skill §4.1, §7.1 | Complete (`Nutrition.AppHost`, typed resource references, launch settings) | Compiled & Verified |
| **Aspire Dashboard Virtualize JS Patch**| Skill §4.1, §7.1 | Complete (4th parameter `SpacerVisibilityReason` patch in `blazor.web.11.js`) | 0 JS Interop Exceptions |
| **HTTP Request/Response Tracing Telemetry**| Skill §4.1, §7.2 | Complete (`HttpPayloadTelemetryMiddleware.cs`, `http.request.body`, `http.response.body`) | Verified in Aspire Traces |
| **GenAI Semantic Tracing & Logging Scopes**| Skill §4.1, §7.2 | Complete (`NutritionTelemetry.cs`, GenAI semantic tags, non-PII logging scopes) | Verified in Aspire Traces & Logs |
| **Identity & Authentication Engine** | Plan §2, §3 | Complete (PBKDF2-HMAC-SHA512, DPDPA Dual-Consent, 6-digit OTP activation) | 100% Passed (Unit Tests) |
| **User Tiers & Entitlements** | Plan §1, §4 | Complete (Free, Basic, Premium, SuperAdmin dynamic limits & feature flags) | 100% Passed (Unit Tests) |
| **Dynamic AI Quota & Mid-Reset** | Plan §4 | Complete (Localized midnight reset, 7d/30d tracking, photo compare gating) | 100% Passed (Eval Harness) |
| **Polly Rate Limiting (OWASP A04)** | Plan §3.3 | Complete (Sliding window on auth endpoints, HTTP 429 response) | 100% Passed (Polly Tests) |
| **SuperAdmin Governance & Telemetry**| Plan §5 | Complete (User management API, anti-lockout protection, AI audit log) | Verified via Tests |
| **Obsidian Dark Auth Gate & UI** | Plan §6 | Complete (Dashboard lock, dual-consent modals, quota HUD, admin console) | Verified in Browser |
| **JWT Cryptographic Authentication & SmartScheme** | Plan §3.4 | Complete (HMAC-SHA256, 24h lifetime, policy forwarding, programmatic token API) | 100% Passed (Unit Tests) |

---

## 4. Traceability Matrix

| User & Clinical Requirement | Architecture Component | Domain Entity / Service | Verification Harness |
|---|---|---|---|
| **Zero-Assumption Intake** | `Nutrition.ProfileService` | `UserProfile.ValidateIntakeCompleteness()` | `ClinicalCalculatorsTests.ZeroAssumptionRule_ThrowsOnMissingMetrics` |
| **Asian-Indian BMI Cutoffs** | `Nutrition.Domain.Clinical` | `ClinicalCalculators.ComputeWhoAsianIndianBmi()` | `ClinicalCalculatorsTests.ComputeWhoAsianIndianBmi_FollowsSouthAsianCutoffs` |
| **Starvation Safety Floor** | `Nutrition.Domain.Clinical` | `ClinicalCalculators.CalculateCaloricBudget()` | `ClinicalCalculatorsTests.CalculateCaloricBudget_EnforcesStarvationFloor_ForFemale` |
| **Hypothyroidism TDEE -12%** | `Nutrition.Domain.Clinical` | `ClinicalCalculators.CalculateCaloricBudget()` | `ClinicalCalculatorsTests.CalculateCaloricBudget_Hypothyroidism_ReducesTdeeBy12Percent` |
| **AI Food Vision with Confidence Gating** | `Nutrition.VisionService` | `IFoodVisionAgent`, `MealsController` | `FoodVisionEvalHarnessTests` |
| **Model Detection Transparency** | `Nutrition.WebGateway` (PWA) | `IndianMealAnalysisResult.DetectedByModel`, `review-modal.html` | Browser Verification Subagent |
| **Quantity Detection & Editable Portions** | `Nutrition.VisionService` & Client | `FoodItemRecord.Quantity`, `review-modal.js` | Browser Verification Subagent |
| **Fiber & Sugar Nutrition Tracking** | `Nutrition.Domain.Model` | `MealLog.TotalFiberGrams`, `MealLog.TotalSugarGrams` | `DailyCalorieLedgerTests` |
| **Logged Meals Food Diary & Excel Export** | `Nutrition.WebGateway` | `analytics-card.html`, `analytics-chart.js` | Browser Verification Subagent |
| **Obsidian Dark Delete Dialog** | `Nutrition.WebGateway` | `delete-meal-modal.html`, `analytics-chart.js` | Browser Verification Subagent |
| **Profile Timezone & Universal UTC Storage**| `Nutrition.Infrastructure` & Domain | `UserProfile.Timezone`, EF Core ValueConverter | `ClinicalDietitianServiceTests` |
| **Universal Obsidian SVG Image Fallbacks** | `Nutrition.WebGateway` | `assets/placeholder-meal.svg`, `main.js` capturing listener | Browser Verification Subagent |
| **Multi-Model Fallback Cascade** | `Nutrition.Infrastructure.AI` | `MicrosoftAgentFoodVisionService` (3-Flash -> 2.5-Flash -> 2.5-Pro) | Live Vision Evals |
| **Continuous Learning on Correction** | `Nutrition.Infrastructure.Data` | `UserCorrectionRecord`, `NutritionDbContext` | Integration Verification |
| **1-Tap Review & Modifiers** | `Nutrition.WebGateway` (PWA) | `review-modal.html`, `review-modal.js` | Browser Verification Subagent |
| **Daily Ledger Recalculation** | `Nutrition.AnalyticsService` | `DailyCalorieLedger.RecalculateLedger()` | `DailyCalorieLedgerTests` |
| **Visual Body & Face Progress** | `Nutrition.WebGateway` (PWA) | `progress-modal.html`, `progress-modal.js` | Browser Verification Subagent |
| **Modular Component Loader** | `Nutrition.WebGateway` (PWA) | `index.html`, `main.js`, `di-container.js` | Browser Verification Subagent |
| **Full HTTP Payload Tracing** | `Nutrition.WebGateway` | `HttpPayloadTelemetryMiddleware` | Aspire Tracing Dashboard Inspection |
| **GenAI Semantic Spans & Logging Scopes** | `Nutrition.Application.Common` | `NutritionTelemetry.ActivitySource`, GenAI semantic conventions | Aspire Traces & Structured Logs |
| **Schema-Safe DB Migration & Comparers** | `Nutrition.Infrastructure.Data` | `NutritionDbContext`, `EfRepository<T>`, `EfUnitOfWork` | Database Startup Verification (0 Errors) |
| **Non-PII Diagnostic Logging** | `Nutrition.Infrastructure.Data` | `EfRepository<T>`, `EfUnitOfWork` sanitized diagnostics | Log Inspection Verification |
| **Zero-PII Git Compliance** | `Nutrition.WebGateway` | `SUPER_ADMIN_EMAIL`, `admin@dietdost.app` configuration | Config & Repo Inspection |
| **DPDPA 2023 Dual Consent** | `Nutrition.Domain.Model.Identity` | `ApplicationUser.TermsAcceptedAtUtc`, `HealthConsentAcceptedAtUtc` | Integration & DB Schema Verification |
| **Polly Sliding Window Limiting** | `Nutrition.WebGateway` | `ResiliencePipelineBuilder.AddRateLimiter`, Auth Middleware | `PollyRateLimitingTests` |
| **Dynamic Tier AI Detection Ceilings** | `Nutrition.Application.Services` | `IAiQuotaService`, `ITierConfigurationService`, `AiQuotaService` | `AiQuotaAndTierServiceTests` |
| **Localized Midnight Reset** | `Nutrition.Infrastructure.Services` | `AiQuotaService.GetLocalizedMidnightUtc()`, `UserProfile.Timezone` | `AiQuotaAndTierServiceTests` |
| **SuperAdmin Anti-Lockout Rules** | `Nutrition.WebGateway.Controllers` | `AdminController.UpdateUserRole()`, `UpdateUserStatus()` | Integration Verification |
| **Tier Feature Gating (Photo/Excel)** | `Nutrition.WebGateway.Controllers` | `MealsController.ExportMeals()`, `ProgressPhotosController.GetComparison()` | 403 Forbidden Response Verification |
| **JWT Bearer Token Authentication** | `Nutrition.Infrastructure.Security` | `IJwtTokenService`, `JwtTokenService` | `JwtAuthenticationTests` |
| **Dual SmartScheme Auth Dispatch** | `Nutrition.WebGateway` | `AddPolicyScheme("SmartScheme")`, `Program.cs` | Integration Verification |



<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261014-068: AI in SDLC, Agentic Workflows Architecture & Day-to-Day Living Synchronization Mandate

> **Date / Timestamp**: 2026-10-14T13:20:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Lead Architect, DevOps / Aspire Expert & AI Coding Agent Pair-Programming  
> **Change Type**: [ARCHITECTURE | GOVERNANCE | DEVOPS | SECURITY]  
> **Affected Subsystems**: [WebGateway | AppHost | Application | DevOps CI/CD | Agentic Workflows | SDD Specifications]  
> **Associated PR & Stack**: PR #<pending> (Branch: `docs/ai-in-sdlc-architecture`, Base: `origin/main`)  
> **Relevant SDDs & Standards**: [`docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md), [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md)  

---

## 1. Executive Summary & Change Rationale

* **Comprehensive Architectural Specification**: Authored and integrated [`docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md) (v1.4.0) defining the comparative evolution across **Traditional SDLC vs. AI-Augmented SDLC vs. Autonomous Agentic SDLC**, role and seniority transformations, 5-phase engineering lifecycles, and Diet-Dost's 3 operational boundary zones.
* **Visual Architectural Rigor**: Generated and embedded 9 high-resolution architectural diagrams in `docs/sdd/images/` covering lifecycle spectrum pillars, agentic design governance, 6-vector developer AI defense, multimodal evaluation harnesses, 8-job DevSecOps pipelines, AIOps telemetry shields, and comparative scorecards.
* **Aspire Artifact Hygiene & Single Source of Truth**: Excluded `.NET Aspire` auto-generated synthesis artifacts (`aspire-output/`, `cae-*.bicep`, `aspire-manifest.json`, `aspire.config.json`) across `.gitignore`, `.dockerignore`, and `.containerignore` to eliminate dual sources of truth and Git drift.
* **Day-to-Day Living Synchronization Mandate**: Formally codified **Rule 17 in [AGENTS.md](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)** and Section 9 in SDD 11 with an 7-point evolution trigger matrix (T-1 to T-7) mandating immediate synchronization whenever the solution's AI models, prompts, agentic workflows, DevSecOps gates, or evaluation metrics evolve.

---

## 2. Context and Problem Statement

As artificial intelligence rapidly transitions from simple inline code autocompletion (Copilot) to autonomous multi-agent state machines, software engineering teams face critical architectural questions:
1. How do roles, seniorities, and responsibilities evolve across Traditional, AI-Augmented, and Agentic lifecycles?
2. Where in the Diet-Dost solution should autonomous agents operate versus where must strict deterministic human-governed code prevail?
3. How can development teams prevent subtle AI bugs (e.g. prompt injection, lazy security disablers, slopsquatting, continuous learned memory poisoning) from contaminating production?
4. How do we ensure that auto-generated orchestration artifacts (`aspire-output/`) and architectural documentation do not drift as the solution evolves day to day?

---

## 3. Decision Drivers

* **Zero Documentation Drift**: Architectural documentation must reflect the live state of code, tools, and CI/CD pipelines at all times.
* **Deterministic Boundary Protection**: Clinical dietetics (ICMR-NIN 2024 / WHO), DPDPA 2023 legal privacy, and cryptographic authorization must remain strictly deterministic (Zone 1) with zero probabilistic LLM drift.
* **Aspire Artifact Cleanliness**: The C# AppHost (`Nutrition.AppHost`) must remain the single source of truth for deployment topology; transient generated Bicep and manifest files must never be committed.
* **Defense-in-Depth**: Multi-layered defense spanning pre-commit hooks (6 AI vectors), CI/CD DevSecOps gates (8 parallel jobs), and runtime edge Prompt Shielding.

---

## 4. Considered Options

* **Option 1: Static Architecture Overview in README**: Document AI tools briefly in `README.md` without deep governance or lifecycle comparison.
  - *Drawbacks*: Lacks engineering rigor, fails to explain role evolution, and offers zero automated governance for agentic workflows.
* **Option 2: Standalone SDD Without Repository Enforcement**: Write SDD 11 but leave maintenance to informal developer habits.
  - *Drawbacks*: Results in rapid documentation drift ("shelfware") as AI models and pipelines change over time.
* **Option 3: Authoritative SDD 11 + 9 Visual Diagrams + Aspire Artifact Hygiene + AGENTS.md Rule 17 Codification (Selected)**: Create comprehensive textbook-grade specification, embed high-resolution visual blueprints, clean version control ignore manifests, and codify mandatory day-to-day synchronization rules in `AGENTS.md`.

---

## 5. Decision Outcome

* **Chosen Option**: **Option 3**.
* **Justification**: Formally codifying the engineering lifecycles and mandating continuous living updates in `AGENTS.md` guarantees that both human contributors and AI agents operate with complete alignment, zero drift, and uncompromised security.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Complete Architectural Clarity**: Clear mental model distinguishing Traditional SDLC, AI-Augmented SDLC, and Autonomous Agentic SDLC for stakeholders, architects, and junior engineers.
* **Repository Cleanliness**: Prevents hundreds of generated Aspire Bicep lines from polluting Git commits and causing merge friction.
* **Continuous Accuracy**: The 7-point evolution trigger matrix ensures SDD 11 remains an active, living document updated alongside every relevant pull request.

### Negative Consequences / Accepted Trade-Offs:
* **Slight PR Authoring Overhead**: Contributors must remember to update SDD 11 and record an atomic ADR fragment when changing AI integrations or CI/CD gates. This is mitigated by automated agentic workflow support.

---

## 7. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Mobile Modernization**: Mobile BFF contracts and local ONNX runtime deployments on Android/iOS directly inherit the 6-vector AI defense rules and evaluation confidence thresholds established in SDD 11.
* **Phase 3 Enterprise Cloud Persistence**: Azure Managed Identity, DPDPA 2023 audit trails, and Azure Cosmos DB telemetry will hook into the OpenTelemetry GenAI semantic conventions codified in Section 6.
* **Phase 4 Continuous Automated CI/CD**: The 8-job DevSecOps pipeline specified in Section 5 serves as the blueprint for upcoming GitHub Actions production workflows.

---

## 8. Verification & Compliance Results

* **Automated Builds & Compilation**:
  - `dotnet build DietDost.slnx`: 0 warnings, 0 errors (.NET 11 target).
* **Ignore Manifest Verification**:
  - `git check-ignore -v src/Nutrition.AppHost/aspire-output/dietdost-storage/dietdost-storage.bicep`: Correctly matched by `.gitignore` line 44 (`**/aspire-output/`).
* **Living Documentation & ADR Registry**:
  - Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md).
  - Referenced in [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md) and [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md).
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`

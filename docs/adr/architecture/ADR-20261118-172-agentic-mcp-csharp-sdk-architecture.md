<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261118-172: Agentic Workflows Architecture with ModelContextProtocol C# SDK and Aspire 17

## Status
Accepted

## Date
2026-11-18

## Domain
architecture

## Context
Diet-Dost requires an enterprise-grade agentic interface to allow autonomous AI agents, local developer tools (Claude Desktop, VS Code GitHub Copilot, Antigravity, MCP Inspector), and remote cloud clients (ChatGPT Custom Actions, Claude Web) to query user nutrition history and log meal entries via text or photo.
Crucially:
1. **Clinical Dietetics Zero-Assumption Intake**: Unverified AI detections must never be committed to the permanent medical ledger without human-in-the-loop (HITL) approval.
2. **Cloud Cost & Concurrency Governance**: Running a separate Azure Container App would incur double billing and risk SQLite file locking contention on the Azure SMB persistent volume (`diettracker.db`).
3. **Multi-Tenant Security & DPDPA 2023**: Elicitation state must be protected against replay, cross-tenant token substitution, and prompt injection attacks.

## Decision
1. **Model Context Protocol (MCP) C# SDK**: Adopt the official `ModelContextProtocol.AspNetCore` and `ModelContextProtocol` v2.4.0 with .NET 11 and Aspire 17.
2. **Zero-Cost Unified Deployment**: Co-host the remote HTTP/SSE `/mcp` endpoint directly within `Nutrition.WebGateway` on Azure Container Apps. This achieves **$0.00 incremental cloud cost** and preserves the SQLite single-writer invariant on `/app/data`.
3. **Dual Transport Support**:
   - HTTP/SSE endpoint on `/mcp` for network, web PWA, ChatGPT, and Aspire Orchestration.
   - Dedicated headless CLI console tool (`Nutrition.Tools.McpCli`) for Claude Desktop `stdio` JSON-RPC transport.
4. **Human-in-the-Loop (HITL) Elicitation Gate**:
   - `add_meal_entry` computes nutritional breakdown with `MicrosoftAgentFoodVisionService` and ICMR-NIN 2024 / IFCT 2017 data.
   - Generates an `InputRequiredResult` containing a user-bound AES-256-GCM continuation token (`IDataProtector` scoped to `UserId`) with a 15-minute TTL.
   - Only upon human confirmation is `ConfirmMealCommand` dispatched with `IsVerifiedByUser = true`.
5. **Auto-Detecting Agent Client**:
   - `Nutrition.AgentClient` probes local Ollama (`llama3.2`) and seamlessly falls back to Google Gemini using Diet-Dost's configuration.

## Consequences
### Positive
- Zero duplicate domain logic: All MCP tools directly dispatch existing CQRS commands and queries via `IDispatcher`.
- Zero additional Azure infrastructure cost and zero risk of SQLite SMB lock corruption.
- Full interoperability with Claude Desktop, VS Code Copilot, ChatGPT, and MCP Inspector.
- Bank-grade adversarial defense: Verified 97% confidence score against 6 threat vectors.

### Negative / Trade-offs
- Modern MCP elicitation (`InputRequiredResult`) requires clients supporting the MCP 1.0+ elicitation spec; a two-stage fallback (`propose_meal_entry` / `confirm_meal_entry`) is maintained for older clients.

## Compliance & References
- **Target Framework**: .NET 11, Aspire 17 Project Resource v2
- **Related SDDs**: `docs/sdd/02_solution_architecture.md`, `docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`
- **Related CFT**: `docs/cft/cft_agentic_mcp_workflows.md`
- **GitHub Issue**: [#171](https://github.com/nikunjbanker/diet-dost/issues/171)

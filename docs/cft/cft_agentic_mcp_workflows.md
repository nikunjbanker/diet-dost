<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Customer & Functional Acceptance Test (CFT): Agentic Workflows with Model Context Protocol (MCP)

> **CFT ID**: `CFT-MCP-001`  
> **Status**: Living & Automated  
> **Target Release**: Phase 2 / .NET 11 / Aspire 17  
> **Target Protocols**: Model Context Protocol (MCP) JSON-RPC 2.0 (HTTP/SSE & Stdio), AGUI  
> **Authoritative Specification**: [ADR-172](../adr/architecture/ADR-20261118-172-agentic-mcp-csharp-sdk-architecture.md), [02_solution_architecture.md](../sdd/02_solution_architecture.md), [11_ai_in_sdlc_and_agentic_workflows_architecture.md](../sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md)

---

## 1. Acceptance Criteria Checklist

| Test ID | Scenario | Given | When | Then | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CFT-MCP-01** | Tool Discovery via MCP Protocol | Authenticated MCP client connects to `/mcp` | Client sends `tools/list` JSON-RPC request | Server returns `view_meal_logs`, `view_daily_ledger`, `add_meal_entry`, `confirm_meal_entry` with descriptions and parameter schemas. | ✅ READY |
| **CFT-MCP-02** | View Meal Logs by Period (Day/Week) | User with seeded demo meals | Client executes `view_meal_logs(period='day')` | Server returns aggregated calories, protein, carbs, fat, fiber, sodium, and detailed item list. | ✅ READY |
| **CFT-MCP-03** | Human-in-the-Loop Meal Logging Elicitation | User describes a meal in natural language | Client executes `add_meal_entry(textDescription="2 rotis, dal, dahi")` | Server does NOT commit to DB; returns `InputRequiredResult` with encrypted 15m continuation token and verification prompt. | ✅ READY |
| **CFT-MCP-04** | Human Confirmation & Ledger Commit | Pending meal elicitation with valid token | User confirms `add_meal_entry(approvalToken, approved='yes')` | Server validates token, dispatches `ConfirmMealCommand` (`IsVerifiedByUser=true`), and records meal in diary. | ✅ READY |
| **CFT-MCP-05** | Human Rejection / Cancellation | Pending meal elicitation with valid token | User rejects `add_meal_entry(approvalToken, approved='no')` | Server returns cancellation message; zero writes made to the health ledger. | ✅ READY |
| **CFT-MCP-06** | Cross-User Token Replay Defense | Token generated for User A (`free@dietdost.app`) | Attacker submits token under User B (`premium@dietdost.app`) | Server rejects decryption due to purpose chain mismatch; returns unauthorized/invalid token error. | ✅ READY |
| **CFT-MCP-07** | Expired Token Rejection | Token older than 15 minutes | User submits expired token | Server rejects execution with token expired message. | ✅ READY |
| **CFT-MCP-08** | OWASP LLM04 Tier Quota Pre-Check | Free tier user exceeds 3 daily AI detections | Client invokes `add_meal_entry` | Server intercepts before calling Gemini; returns quota exceeded response with tier upgrade guidance. | ✅ READY |
| **CFT-MCP-09** | Local Stdio Transport (Claude Desktop) | Local workstation without active web browser | Claude Desktop launches CLI tool via `stdin`/`stdout` | Server processes JSON-RPC frames directly over standard I/O streams. | ✅ READY |
| **CFT-MCP-10** | Dual Provider Agent Client Auto-Detection | `Nutrition.AgentClient` test harness launched | Local Ollama is offline | Client seamlessly falls back to Google Gemini using Diet-Dost credentials and runs full loop. | ✅ READY |

---

## 2. Test Execution Commands

### Run Automated Unit & Security Tests
```powershell
dotnet test tests/Nutrition.Mcp.Tests/Nutrition.Mcp.Tests.csproj --configuration Release
```

### Run Live Multi-Tier Verification Suite
```powershell
pwsh -File tests/validate_e2e_tiers.ps1
```

### Comprehensive Step-by-Step Developer Testing Guide
For a full developer walkthrough across Unit Tests, `Nutrition.AgentClient`, Claude Desktop, Cursor, and MCP Inspector, consult:
- **[Local MCP Testing & Development Guide](../LOCAL_MCP_TESTING_AND_DEVELOPMENT_GUIDE.md)**


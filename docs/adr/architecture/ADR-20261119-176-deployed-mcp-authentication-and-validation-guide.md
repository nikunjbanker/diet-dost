<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261119-176: Deployed Remote MCP Authentication, Stream Negotiation, and CLI Auto-Reconciliation

- **Status**: Accepted
- **Date**: 2026-11-19
- **Domain**: architecture
- **Deciders**: Solution Architecture, AI Agentic SDLC, Clinical Dietetics Engineering
- **Supercedes**: None
- **Related PR**: Pending
- **Closes Issue**: #179

---

## 1. Context & Problem Statement

Following the establishment of the local Model Context Protocol developer guide ([ADR-175](ADR-20261119-175-local-mcp-developer-and-testing-guide.md)), testing and validation procedures for the cloud-deployed remote MCP server (`https://dev.diet-dost.in/mcp` and `https://diet-dost.in/mcp`) required formal codification:
1. **OWASP ASVS Authentication Gating**: In cloud environments, `mcpEndpoint.RequireAuthorization()` is active. Callers must authenticate via Diet-Dost's Dual SmartScheme (JWT Bearer tokens or cookies); unauthenticated callers receive HTTP 401 Unauthorized. Developers required an explicit, step-by-step authentication playbook using `/api/auth/login`.
2. **MCP HTTP Stream Transport Negotiation**: The MCP 2024-11-05 Stream Transport specification mandates that clients provide `Accept: application/json, text/event-stream`. Calling the endpoint without `text/event-stream` yields HTTP 406 Not Acceptable.
3. **Data Protection Provider in MCP DI Extensions**: Activation of `MealTools` requires `IMcpContinuationTokenProtector`, which depends on `IDataProtectionProvider`. In generic hosts (`Nutrition.Tools.McpCli`), data protection was not registered automatically, causing tool calls to fail with DI resolution errors.
4. **Local SQLite Schema Reconciliation**: Standalone CLI execution (`Nutrition.Tools.McpCli`) on legacy SQLite files lacked automated database bootstrapping and schema reconciliation, failing when accessing new columns (`HealthScoreStatusText`).
5. **Bidirectional Stdio Process Inspection**: Piping JSON strings directly into `dotnet run` sends EOF on `stdin`, closing the process before `stdout` can be consumed by PowerShell.

---

## 2. Decision & Implementation

1. **Codify Deployed Remote MCP Validation Guide**:
   Expanded [`docs/LOCAL_MCP_TESTING_AND_DEVELOPMENT_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/LOCAL_MCP_TESTING_AND_DEVELOPMENT_GUIDE.md) into a unified Local and Deployed Testing Guide, adding **Section 8: Testing & Validating the Deployed Remote MCP Server (with Authentication)**:
   - Detailed authentication workflow against `POST /api/auth/login` using `emailOrMobile` and password credentials.
   - Codified pre-seeded tier accounts (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`).
   - Provided an automated PowerShell verification script performing end-to-end authentication, SSE event parsing, tools discovery, and `estimate_food_portion` tool execution.
   - Codified adversarial security negative test cases (HTTP 401 Unauthorized for missing tokens, HTTP 406 Not Acceptable for missing stream accept headers).
   - Documented remote desktop client integration via `mcp-proxy` for Claude Desktop and Cursor.

2. **Register DataProtection in MCP Extensions**:
   Updated `src/Nutrition.Mcp/McpRegistrationExtensions.cs` to invoke `services.AddDataProtection();` in both `AddDietDostMcpServer` and `AddDietDostMcpStdioServer`. Guarantees that `IMcpContinuationTokenProtector` resolves deterministically across all host topologies (WebGateway, Generic Host, Stdio CLI, test harnesses).

3. **Bootstrap Local SQLite Database in CLI Host**:
   Updated `src/Nutrition.Tools.McpCli/Program.cs` to ensure database creation and apply idempotent column reconciliation on startup (`EnsureCreatedAsync()`, `HealthScoreStatusText`, `TargetSugarGrams`, `ConsumedSugarGrams`).

4. **Codify Bidirectional PowerShell Inspection in Runbook**:
   Updated Section 7 to provide a native bidirectional `System.Diagnostics.Process` script that keeps `stdin` open, synchronously consumes `stdout`, and renders registered tools as a formatted PowerShell table.

---

## 3. Consequences & Verification

- **End-to-End Validation**: Authenticated tools discovery and tool execution verified live on `https://dev.diet-dost.in/mcp` (HTTP 200 with SSE stream event).
- **Security Compliance**: Negative adversarial checks verified: unauthenticated requests return HTTP 401; missing accept headers return HTTP 406.
- **Automated Test Suite**: All 504 unit, integration, and security tests pass cleanly (`Nutrition.Domain.Tests`: 65, `Nutrition.Mcp.Tests`: 13, `Nutrition.EvalHarness.Tests`: 426).
- **Zero Documentation Drift**: ADR, index, and living guide remain 100% synchronized.

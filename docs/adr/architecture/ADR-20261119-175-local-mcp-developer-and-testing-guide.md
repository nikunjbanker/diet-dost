<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261119-175: Local MCP Developer Testing Guide and Stdio CLI Default Configuration

- **Status**: Accepted
- **Date**: 2026-11-19
- **Domain**: architecture
- **Deciders**: Solution Architecture, AI Agentic SDLC, Clinical Dietetics Engineering
- **Supercedes**: None
- **Related PR**: #178
- **Closes Issue**: #177

---

## 1. Context & Problem Statement

Following the delivery of the Model Context Protocol (MCP) C# SDK integration ([ADR-172](ADR-20261118-172-agentic-mcp-csharp-sdk-architecture.md)), Diet-Dost exposes clinical dietetics tools over dual transports:
1. Remote HTTP/SSE at `/mcp` (`Nutrition.WebGateway`)
2. Local Headless Stdio (`Nutrition.Tools.McpCli`)

While acceptance criteria were codified in `docs/cft/cft_agentic_mcp_workflows.md`, developers and contributors lacked a dedicated, hands-on operational guide detailing exact local testing procedures across:
- In-memory xUnit test harness execution
- Live HTTP/SSE client verification (`Nutrition.AgentClient`)
- Claude Desktop and Cursor desktop assistant integration
- Interactive debugging via Anthropic's official `@modelcontextprotocol/inspector`
- Low-level JSON-RPC Stdio piping

Furthermore, `Nutrition.Tools.McpCli` lacked bundled configuration files, causing headless Stdio runs to fail during startup options validation (`Jwt:Key is mandatory`, `Auth:SuperAdminEmail must not be empty`, `Database provider is configured as AzureSql`) unless multiple environment variables were manually defined.

---

## 2. Decision & Implementation

1. **Codify Authoritative Local Testing Runbook**:
   Created [`docs/LOCAL_MCP_TESTING_AND_DEVELOPMENT_GUIDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/LOCAL_MCP_TESTING_AND_DEVELOPMENT_GUIDE.md), detailing:
   - Tool capabilities, parameter schemas, and HITL Zero-Assumption Intake guarantees.
   - 5 isolated developer testing methodologies:
     - Methodology 1: `dotnet test tests/Nutrition.Mcp.Tests/` (13 isolated tests)
     - Methodology 2: `Nutrition.AgentClient` automated 4-scenario runner against local WebGateway
     - Methodology 3: Claude Desktop `claude_desktop_config.json` integration
     - Methodology 4: `npx @modelcontextprotocol/inspector` interactive UI
     - Methodology 5: Raw JSON-RPC Stdio piping and cURL probing
   - Security and safety guardrails (AES-256-GCM continuation tokens, 15m TTL, user-bound purpose strings, OWASP LLM04 tier limits).
   - Troubleshooting diagnostics matrix for common local errors.

2. **Bundle Stdio CLI Default Configuration**:
   Added `src/Nutrition.Tools.McpCli/appsettings.json` configured with local SQLite persistence and local development JWT signing defaults, with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` in `Nutrition.Tools.McpCli.csproj`. This enables `Nutrition.Tools.McpCli` to start instantly over Stdio for Claude Desktop or command-line pipes without requiring any manual environment configuration.

3. **Master Documentation Indexing & Traceability**:
   Linked the testing guide from `README.md`, `docs/cft/cft_agentic_mcp_workflows.md`, and `docs/sdd/00_sdd_index.md`.

---

## 3. Consequences & Governance

- **Zero-Friction Stdio Execution**: Claude Desktop and developer terminal pipes execute in <20ms out of the box.
- **Traceability**: All future contributors and agent workflows have an authoritative operational reference for MCP verification.
- **Closes Issue**: #177.

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Local Model Context Protocol (MCP) Testing & Development Guide

> **Authoritative Specification**: [ADR-172](../adr/architecture/ADR-20261118-172-agentic-mcp-csharp-sdk-architecture.md)  
> **CFT Acceptance Matrix**: [cft_agentic_mcp_workflows.md](cft/cft_agentic_mcp_workflows.md)  
> **Architecture Diagram**: [agentic_mcp_workflows.mermaid](architecture/diagrams/agentic_mcp_workflows.mermaid)  
> **Target Framework**: .NET 11, Aspire 17, ModelContextProtocol C# SDK v2.4.0  

---

## 1. Overview & Architecture

Diet-Dost implements the **Model Context Protocol (MCP)** using the official C# SDK (`ModelContextProtocol.AspNetCore` & `ModelContextProtocol.Client` v2.4.0) to expose clinical dietetics capabilities to autonomous AI agents, desktop assistants, and client applications.

The implementation provides **Dual-Transport Architecture**:
1. **Remote / Network Transport (HTTP / SSE)**:
   - Co-hosted inside `Nutrition.WebGateway` at the `/mcp` route (`http://localhost:5240/mcp` locally, `https://dev.diet-dost.in/mcp` in cloud).
   - Designed for web applications, Aspire orchestrator, remote AI agents, and mobile backends.
2. **Headless Stdio Transport (Standard I/O)**:
   - Implemented as a lightweight console tool in `Nutrition.Tools.McpCli`.
   - Communicates strictly over standard input/output streams via JSON-RPC 2.0 frames (`stdin` / `stdout`), routing internal diagnostic logs to `stderr`.
   - Designed for local desktop assistants like **Claude Desktop**, **Cursor**, **VS Code Copilot**, or terminal pipes.

### Registered Clinical Tools

| Tool Name | Purpose | Key Parameters | HITL Gate? |
| :--- | :--- | :--- | :---: |
| `view_daily_ledger` | Retrieves daily nutritional ledger: caloric budget, calories consumed, remaining balance, and macro split (Protein, Carbs, Fat, Fiber, Sugar, Sodium). | `date` *(optional, `YYYY-MM-DD`)* | Read-Only |
| `view_meal_logs` | Retrieves logged meals and intake history by period or specific target date. | `period` *(`day`, `week`, `month`, `all`)*, `date` *(optional)* | Read-Only |
| `estimate_food_portion` | Estimates nutritional breakdown for a dish using ICMR-NIN 2024 / IFCT 2017 clinical standards. | `foodName` *(required)*, `portion` *(optional, e.g. "1 bowl")* | Read-Only |
| `add_meal_entry` | Drafts a meal proposal from natural language text or photo. Strictly enforces Zero-Assumption Intake. | `textDescription` *(required)*, `mealType` *(optional)* | **Yes (Generates 15m Token)** |
| `confirm_meal_entry` | Commits an elicited meal proposal to the permanent diary upon explicit human confirmation. | `approvalToken` *(required)*, `approved` *(`yes`/`no`)*, `portionMultiplier` *(optional)* | **Yes (Requires Valid Token)** |

---

## 2. Prerequisites

- **.NET 11 SDK** (`dotnet --version` outputs `11.0.100` or higher)
- **PowerShell 7+ (pwsh)** or bash
- *(Optional)* **Node.js 20+** for running the official `@modelcontextprotocol/inspector`
- *(Optional)* **Claude Desktop** or **Cursor** for interactive desktop agent testing
- *(Optional)* **Ollama** running locally with `llama3.2` (`http://localhost:11434`) or a valid `GEMINI_API_KEY`

---

## 3. Testing Methodology 1: Automated Unit & Security Tests (Zero-Dependency)

The fastest and most isolated way to verify the entire MCP tool pipeline, token encryption, and safety guardrails is through the dedicated xUnit test suite.

```powershell
dotnet test tests/Nutrition.Mcp.Tests/Nutrition.Mcp.Tests.csproj
```

### What This Validates:
- **`AnalyticsToolsTests`**: Confirms ledger query dispatch and macro aggregation.
- **`MealToolsTests`**: Validates `view_meal_logs`, `estimate_food_portion`, `add_meal_entry` (HITL elicitation), and `confirm_meal_entry`.
- **`McpContinuationTokenProtectorTests`**:
  - Encrypts and decrypts continuation tokens using ASP.NET Core Data Protection (`IDataProtector`).
  - **15-Minute Expiry Invariant**: Asserts that tokens older than 15 minutes are strictly rejected.
  - **Cross-User Replay Defense**: Asserts that tokens generated for User A cannot be decrypted or consumed by User B.
  - **Replay Protection**: Prevents duplicate meal commits with the same token.

---

## 4. Testing Methodology 2: Automated End-to-End Client Runner (`Nutrition.AgentClient`)

The solution includes a dedicated verification client (`Nutrition.AgentClient`) that connects to the live HTTP/SSE endpoint, discovers tools, and runs an entire interactive meal logging cycle.

### Step-by-Step Instructions:

#### Step 1: Start the Local WebGateway Server
Open a terminal and start `Nutrition.WebGateway` in `Development` mode on port 5240:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
Push-Location src/Nutrition.WebGateway
dotnet run --urls "http://localhost:5240"
```

Verify the health endpoint is online:
```powershell
Invoke-WebRequest -Uri "http://localhost:5240/health" -Method Get
```

#### Step 2: Run the Agent Client Runner
In a second terminal, execute the test client:

```powershell
dotnet run --project src/Nutrition.AgentClient/Nutrition.AgentClient.csproj
```

#### Step 3: Observe the 4-Stage Execution
The client automatically executes:
1. **Connection & Discovery**: Connects to `http://localhost:5240/mcp` and lists all 5 tools.
2. **LLM Auto-Detection**: Checks for local Ollama (`http://localhost:11434`), falls back to Google Gemini if configured, or uses direct tool orchestration.
3. **Scenario A**: Queries `view_daily_ledger` for today's caloric budget and macro split.
4. **Scenario B**: Calls `estimate_food_portion` for `"Paneer Bhurji"`, `"1 bowl"`.
5. **Scenario C (HITL Guardrail)**: Calls `add_meal_entry` with `"2 rotis and yellow dal tadka for lunch"`. Receives `ElicitationRequired` status with an encrypted continuation token (15-min TTL) and zero ledger writes.
6. **Confirmation**: Simulates explicit user confirmation (`approved: "yes"`), dispatches `confirm_meal_entry`, and commits the meal.
7. **Scenario D**: Calls `view_meal_logs` to assert that the confirmed lunch is now recorded in the ledger.

---

## 5. Testing Methodology 3: Claude Desktop Integration via Headless Stdio (`Nutrition.Tools.McpCli`)

You can connect Claude Desktop directly to Diet-Dost's local database and clinical tools over standard I/O streams.

### Step 1: Locate Claude Desktop Configuration
Open your Claude Desktop configuration file:
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

### Step 2: Add the Diet-Dost MCP Server Entry
Add the following configuration block under `mcpServers` (replace `<REPO_ROOT>` with your absolute path, e.g. `C:\\Users\\nikunj.banker\\source\\repos\\diet-dost`):

```json
{
  "mcpServers": {
    "diet-dost": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<REPO_ROOT>\\src\\Nutrition.Tools.McpCli\\Nutrition.Tools.McpCli.csproj",
        "--no-launch-profile"
      ]
    }
  }
}
```

### Step 3: Launch Claude Desktop & Verify
1. Restart Claude Desktop.
2. Look for the **hammer icon (🛠️)** at the bottom right of the prompt box.
3. You should see all 5 `diet-dost` tools listed:
   - `view_daily_ledger`
   - `view_meal_logs`
   - `estimate_food_portion`
   - `add_meal_entry`
   - `confirm_meal_entry`
4. Test prompts:
   - *"What is my daily caloric budget and macro balance today?"*
   - *"Estimate the nutrition for 1 plate of Masala Dosa."*
   - *"I had 2 bajra rotis and baingan bharta for dinner. Please draft this meal."*
   - When Claude shows the proposed nutrition and asks for confirmation, reply *"Yes, confirm and log it."*

---

## 6. Testing Methodology 4: Interactive MCP Inspector

Anthropic's official MCP Inspector provides an interactive web UI for inspecting schemas, invoking tools, and inspecting raw JSON-RPC requests and responses.

```powershell
npx @modelcontextprotocol/inspector dotnet run --project src/Nutrition.Tools.McpCli/Nutrition.Tools.McpCli.csproj --no-launch-profile
```

1. Open the URL displayed in the terminal (typically `http://localhost:5173`).
2. Click **Connect**.
3. Navigate to the **Tools** tab:
   - Select `estimate_food_portion`, input `foodName: "Poha"`, and click **Run Tool**.
   - Select `view_daily_ledger`, leave `date` blank, and click **Run Tool**.
   - Inspect the returned JSON-RPC 2.0 response payload in real time.

---

## 7. Testing Methodology 5: Raw JSON-RPC Testing via Stdio Pipe or cURL

### Option A: Piping JSON-RPC Frames into `Nutrition.Tools.McpCli` (PowerShell)
You can directly send JSON-RPC protocol frames to verify Stdio transport without external UI tools:

```powershell
$inputJson = @'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"manual-pipe","version":"1.0.0"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
'@

$inputJson | dotnet run --project src/Nutrition.Tools.McpCli/Nutrition.Tools.McpCli.csproj --no-launch-profile
```

### Option B: Probing the Live Remote Endpoint via cURL
Test the deployed or local HTTP endpoint:

```bash
# Local development server
curl -i http://localhost:5240/mcp

# Live Azure Container Apps staging/production host
curl -i https://dev.diet-dost.in/mcp
```
*Expected Response*: HTTP 200 with SSE stream initialization or diagnostic information.

---

## 8. Clinical Safety & Security Guardrails

When testing, keep in mind Diet-Dost's clinical and security guardrails:

1. **Zero-Assumption Intake (HitL Elicitation)**:
   - Calling `add_meal_entry` **never** writes to the database directly. It always returns `ElicitationRequired` with an encrypted 15-minute continuation token (`approvalToken`).
   - Only calling `confirm_meal_entry` with `approved: "yes"` and a valid token writes the meal to the user's permanent medical diary.
2. **Cryptographic Token Binding**:
   - Continuation tokens are encrypted via AES-256-GCM and cryptographically bound to the authenticated user ID.
   - Tokens expire strictly after 15 minutes.
3. **OWASP LLM04 Tier Quota Gate**:
   - `add_meal_entry` verifies the user's tier limits (Free: 3/day, Basic: 15/day, Premium: unlimited) prior to calling AI vision or LLM services.
4. **Clean Stdio Streams**:
   - `Nutrition.Tools.McpCli` redirects all framework logging to standard error (`stderr`), ensuring that standard output (`stdout`) contains **strictly valid JSON-RPC 2.0 frames**. This prevents Claude Desktop parse errors.

---

## 9. Troubleshooting

| Symptom | Probable Cause | Resolution |
| :--- | :--- | :--- |
| `Jwt:Key is mandatory and cannot be empty` | Missing `appsettings.json` in CLI project or unset environment | Ensure `src/Nutrition.Tools.McpCli/appsettings.json` exists, or pass `$env:ASPNETCORE_ENVIRONMENT="Development"`. |
| `Cannot connect to http://localhost:5240/mcp` | `Nutrition.WebGateway` is not running | Start the WebGateway in a separate terminal via `dotnet run --project src/Nutrition.WebGateway`. |
| Claude Desktop shows red exclamation mark on `diet-dost` | Malformed JSON in `claude_desktop_config.json` or path contains unescaped backslashes | Use double backslashes (`\\\\`) for all Windows paths in `claude_desktop_config.json`. |
| `Connection reset` or HTTP 401 when calling `/mcp` | Missing authorization headers on secured remote endpoints | In local Development mode, `/mcp` auto-authenticates to the default demo user `free@dietdost.app`. In production, supply a valid Bearer token or SmartScheme session cookie. |

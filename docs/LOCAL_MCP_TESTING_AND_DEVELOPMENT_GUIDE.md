<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Model Context Protocol (MCP) Local & Deployed Testing Guide

> **Authoritative Specification**: [ADR-172](../adr/architecture/ADR-20261118-172-agentic-mcp-csharp-sdk-architecture.md) & [ADR-175](../adr/architecture/ADR-20261119-175-local-mcp-developer-and-testing-guide.md)  
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

## 7. Testing Methodology 5: Interactive JSON-RPC Testing via Stdio Process (`Nutrition.Tools.McpCli`)

You can send raw JSON-RPC 2.0 frames directly into the headless Stdio transport. Because `Nutrition.Tools.McpCli` separates diagnostic logging to `stderr` and JSON-RPC responses to `stdout`, use bidirectional process streaming in PowerShell to capture and format responses:

### Interactive PowerShell Script (Tools Discovery & Execution)

```powershell
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --project src/Nutrition.Tools.McpCli/Nutrition.Tools.McpCli.csproj --no-launch-profile"
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$p = [System.Diagnostics.Process]::Start($psi)

# 1. Handshake: initialize
$p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"manual-pipe","version":"1.0.0"}}}')
$initResponse = $p.StandardOutput.ReadLine()

# 2. Tool Discovery: tools/list
$p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}')
$toolsResponse = $p.StandardOutput.ReadLine()

# 3. Clean Shutdown
$p.Kill()

# 4. Format registered tools as a console table
($toolsResponse | ConvertFrom-Json).result.tools | Format-Table name, description
```

> [!NOTE]
> **Stream Separation & Pipeline EOF**:
> If you pipe strings directly (e.g. `$inputJson | dotnet run`), PowerShell closes `stdin` immediately upon streaming the text. The MCP SDK senses EOF and immediately initiates host shutdown. Diagnostic logs appear on `stderr`, but the process terminates before `stdout` is captured. The bidirectional script above keeps `stdin` open and synchronously reads `stdout` lines.

---

## 8. Testing Methodology 6: Testing & Validating the Deployed Remote MCP Server (with Authentication)

In cloud environments (Azure Container Apps / Kubernetes), the remote endpoint at **`https://dev.diet-dost.in/mcp`** (or production `https://diet-dost.in/mcp`) is protected by **OWASP ASVS authorization policies** (`mcpEndpoint.RequireAuthorization()`).

Unlike local development mode, any caller accessing the deployed MCP endpoint **must authenticate** via Diet-Dost's Dual SmartScheme (JWT Bearer Token RFC 7519 or `DietDost_Session` cookie).

### Protocol & Header Requirements

| Requirement | Value / Format | Purpose |
| :--- | :--- | :--- |
| **Endpoint URL** | `https://dev.diet-dost.in/mcp` | Deployed ASP.NET Core MCP route |
| **Authorization** | `Bearer <JWT_TOKEN>` | Cryptographic user identification & tier enforcement |
| **Accept** | `application/json, text/event-stream` | **Mandatory** per MCP HTTP Stream spec (omitting returns HTTP 406) |
| **Content-Type** | `application/json` | JSON-RPC 2.0 payload format |

---

### Step 1: Obtain an Authenticated JWT Bearer Token

Authenticate against the Identity Gateway using any seeded demo user account:

```powershell
$loginBody = @{
    emailOrMobile = "premium@dietdost.app"
    password      = "DietDost@Demo2026!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "https://dev.diet-dost.in/api/auth/login" `
    -Method Post -Body $loginBody -ContentType "application/json"

$jwt = $loginResponse.token
Write-Host "Authenticated as: $($loginResponse.user.email) (Tier: $($loginResponse.user.tier))"
```

#### Demo Account Roster for Testing
| Account | Password | Tier | Quota Limit |
| :--- | :--- | :--- | :--- |
| `free@dietdost.app` | `DietDost@Demo2026!` | Free | 3 AI entries / day |
| `basic@dietdost.app` | `DietDost@Demo2026!` | Basic | 15 AI entries / day |
| `premium@dietdost.app` | `DietDost@Demo2026!` | Premium | Unlimited |
| `admin.demo@dietdost.app` | `DietDost@Demo2026!` | Admin | Administrative Access |
| `superadmin@dietdost.app` | `DietDost@Demo2026!` | SuperAdmin | Full System Authority |

---

### Step 2: Automated End-to-End Deployed Validation Script

Execute this complete PowerShell script to authenticate, list tools, and invoke `estimate_food_portion` against the live deployed cloud host:

```powershell
# 1. Login to obtain JWT Token
$loginBody = @{ emailOrMobile = "premium@dietdost.app"; password = "DietDost@Demo2026!" } | ConvertTo-Json
$auth = Invoke-RestMethod -Uri "https://dev.diet-dost.in/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$jwt = $auth.token

$headers = @{
    Authorization = "Bearer $jwt"
    Accept        = "application/json, text/event-stream"
}

# 2. Query registered tools via MCP Stream Transport
$toolsPayload = @{ jsonrpc = "2.0"; id = 1; method = "tools/list"; params = @{} } | ConvertTo-Json
$toolsRaw = Invoke-RestMethod -Uri "https://dev.diet-dost.in/mcp" -Method Post -Headers $headers -Body $toolsPayload -ContentType "application/json"

Write-Host "`nDiscovered Clinical Tools on Deployed Server:"
$toolsJson = ($toolsRaw -split "`n" | Where-Object { $_ -match "^data:" }) -replace "^data:\s*", "" | ConvertFrom-Json
$toolsJson.result.tools | Format-Table name, description

# 3. Call tool: estimate_food_portion
$callPayload = @{
    jsonrpc = "2.0"
    id      = 2
    method  = "tools/call"
    params  = @{
        name      = "estimate_food_portion"
        arguments = @{ foodName = "Crispy Masala Dosa"; portion = "1 Dosa (150g)" }
    }
} | ConvertTo-Json -Depth 5

$callRaw = Invoke-RestMethod -Uri "https://dev.diet-dost.in/mcp" -Method Post -Headers $headers -Body $callPayload -ContentType "application/json"
$callJson = ($callRaw -split "`n" | Where-Object { $_ -match "^data:" }) -replace "^data:\s*", "" | ConvertFrom-Json

Write-Host "Tool Execution Result:"
$callJson.result.content[0].text
```

---

### Step 3: Adversarial Security & Negative Validation Tests

Verify that OWASP ASVS access controls and protocol negotiation guards reject unauthorized or malformed requests:

1. **Unauthenticated Access (Missing Bearer Token)**:
   ```bash
   curl -i -X POST https://dev.diet-dost.in/mcp \
     -H "Content-Type: application/json" \
     -H "Accept: application/json, text/event-stream" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}'
   ```
   *Expected Response*: **`HTTP/1.1 401 Unauthorized`** (Protected against unauthorized access).

2. **Protocol Negotiation Guard (Missing `text/event-stream` in Accept Header)**:
   ```bash
   curl -i -X POST https://dev.diet-dost.in/mcp \
     -H "Authorization: Bearer <TOKEN>" \
     -H "Content-Type: application/json" \
     -H "Accept: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{...}}'
   ```
   *Expected Response*: **`HTTP/1.1 406 Not Acceptable`** (`Client must accept both application/json and text/event-stream`).

---

### Step 4: Connecting Remote Desktop Clients (Claude Desktop / Cursor)

To connect Claude Desktop to the live cloud endpoint with authentication, configure an SSE-to-Stdio proxy (such as `mcp-proxy`) in `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "diet-dost-cloud": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-proxy",
        "--transport", "sse",
        "--url", "https://dev.diet-dost.in/mcp",
        "--header", "Authorization=Bearer <YOUR_GENERATED_JWT_TOKEN>"
      ]
    }
  }
}
```

---

## 9. Clinical Safety & Security Guardrails

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

## 10. Troubleshooting

| Symptom | Probable Cause | Resolution |
| :--- | :--- | :--- |
| `HTTP 401 Unauthorized` on deployed `/mcp` | Missing or expired JWT Bearer token | Call `POST /api/auth/login` to obtain a fresh token and pass `Authorization: Bearer <token>`. |
| `HTTP 406 Not Acceptable` on deployed `/mcp` | Missing `text/event-stream` in HTTP `Accept` header | Set `Accept: application/json, text/event-stream` on all MCP HTTP requests. |
| `HTTP 400 Bad Request` on `/api/auth/login` | Incorrect JSON property name | Use `emailOrMobile` instead of `email` in the login payload. |
| `Jwt:Key is mandatory and cannot be empty` | Missing `appsettings.json` in CLI project or unset environment | Ensure `src/Nutrition.Tools.McpCli/appsettings.json` exists, or pass `$env:ASPNETCORE_ENVIRONMENT="Development"`. |
| `Cannot connect to http://localhost:5240/mcp` | `Nutrition.WebGateway` is not running | Start the WebGateway in a separate terminal via `dotnet run --project src/Nutrition.WebGateway`. |
| Claude Desktop shows red exclamation mark on `diet-dost` | Malformed JSON in `claude_desktop_config.json` or path contains unescaped backslashes | Use double backslashes (`\\\\`) for all Windows paths in `claude_desktop_config.json`. |
| `no such column: l.HealthScoreStatusText` | Outdated SQLite database file | `Nutrition.Tools.McpCli` now automatically applies schema reconciliation on startup. |

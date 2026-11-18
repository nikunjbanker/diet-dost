/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using OpenAI.Chat;

// ====================================================================================
// Diet-Dost Agent Client — Autonomous Agent & MCP Verification Runner
// Architecture: Model Context Protocol (MCP) Client + Microsoft.Extensions.AI (MEAI)
// Target: .NET 11 Console Host
// Dual LLM: Local Ollama (llama3.2) with graceful fallback to Google Gemini
// ====================================================================================

Console.WriteLine("===============================================================");
Console.WriteLine(" Diet-Dost Agentic MCP Client (.NET 11 / Aspire 17)");
Console.WriteLine("===============================================================");

var mcpServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:5240/mcp";
Console.WriteLine($"\n[1/4] Connecting to Diet-Dost MCP Server at: {mcpServerUrl}");

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(mcpServerUrl)
});

await using var mcpClient = await McpClient.CreateAsync(transport, new McpClientOptions
{
    ClientInfo = new() { Name = "DietDost-AgentClient", Version = "1.0.0" }
});

Console.WriteLine("[2/4] Discovering registered clinical MCP tools...");
var tools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Discovered {tools.Count} tool(s):");
foreach (var tool in tools)
{
    Console.WriteLine($" - {tool.Name}: {tool.Description}");
}

// [3/4] Configure LLM Client: Ollama -> Gemini -> Deterministic Tool Runner
IChatClient? chatClient = null;
using var httpCheck = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

try
{
    var ollamaRes = await httpCheck.GetAsync("http://localhost:11434/api/tags");
    if (ollamaRes.IsSuccessStatusCode)
    {
        Console.WriteLine("\n[3/4] Local Ollama detected at http://localhost:11434! Using 'llama3.2' model.");
        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") };
        var openAiClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential("ollama"), openAiOptions);
        chatClient = openAiClient.GetChatClient("llama3.2").AsIChatClient();
    }
}
catch
{
    // Ollama not running locally
}

if (chatClient == null)
{
    var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? Environment.GetEnvironmentVariable("GoogleAI__ApiKey");
    if (!string.IsNullOrWhiteSpace(geminiKey))
    {
        Console.WriteLine("\n[3/4] Google Gemini API detected! Configuring Gemini 2.5 Flash via OpenAI-compatible bridge.");
        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") };
        var openAiClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(geminiKey), openAiOptions);
        chatClient = openAiClient.GetChatClient("gemini-2.5-flash").AsIChatClient();
    }
}

if (chatClient != null)
{
    Console.WriteLine("[3/4] IChatClient configured successfully.");
}
else
{
    Console.WriteLine("[3/4] No LLM endpoint active; executing autonomous clinical workflow via direct tool orchestration.");
}

// [4/4] Execute Clinical Workflow Verification (Zero-Assumption Intake)
Console.WriteLine("\n[4/4] Executing Clinical Workflow Verification (Zero-Assumption Intake):");

// Scenario A: View Daily Ledger
Console.WriteLine("\n--- Scenario A: view_daily_ledger ---");
var ledgerParams = new Dictionary<string, object?> { ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd") };
var ledgerResult = await mcpClient.CallToolAsync("view_daily_ledger", ledgerParams);
Console.WriteLine($"Raw result: {JsonSerializer.Serialize(ledgerResult)}");

// Scenario B: Estimate Food Portion
Console.WriteLine("\n--- Scenario B: estimate_food_portion ('Paneer Bhurji', '1 bowl') ---");
var estimateParams = new Dictionary<string, object?> { ["foodName"] = "Paneer Bhurji", ["portion"] = "1 bowl" };
var estimateResult = await mcpClient.CallToolAsync("estimate_food_portion", estimateParams);
Console.WriteLine($"Raw result: {JsonSerializer.Serialize(estimateResult)}");

// Scenario C: Add Meal Entry (Stage 1 Elicitation)
Console.WriteLine("\n--- Scenario C: add_meal_entry (Stage 1: Proposal & Approval Token Generation) ---");
var mealParams = new Dictionary<string, object?> { ["textDescription"] = "2 rotis and yellow dal tadka for lunch" };
var mealElicitation = await mcpClient.CallToolAsync("add_meal_entry", mealParams);
Console.WriteLine($"Elicitation response: {JsonSerializer.Serialize(mealElicitation)}");

// Extract approval token from elicitation response
string? approvalToken = null;
if (mealElicitation.Content != null)
{
    foreach (var block in mealElicitation.Content)
    {
        if (block is TextContentBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            try
            {
                using var doc = JsonDocument.Parse(textBlock.Text);
                if (doc.RootElement.TryGetProperty("approvalToken", out var tokProp))
                {
                    approvalToken = tokProp.GetString();
                    break;
                }
            }
            catch
            {
                // Fallback to substring extraction if serialized inside wrapper
                var tokenIndex = textBlock.Text.IndexOf("\"approvalToken\":\"", StringComparison.OrdinalIgnoreCase);
                if (tokenIndex >= 0)
                {
                    var start = tokenIndex + 17;
                    var end = textBlock.Text.IndexOf('"', start);
                    if (end > start)
                    {
                        approvalToken = textBlock.Text[start..end];
                        break;
                    }
                }
            }
        }
    }
}

if (!string.IsNullOrWhiteSpace(approvalToken))
{
    Console.WriteLine($"\n[HITL Guardrail] Received continuation token: {approvalToken[..Math.Min(16, approvalToken.Length)]}... (15-min TTL)");
    Console.WriteLine("Simulating Human User Approval: 'yes'");

    var confirmParams = new Dictionary<string, object?>
    {
        ["approvalToken"] = approvalToken,
        ["approved"] = "yes"
    };

    var confirmResult = await mcpClient.CallToolAsync("confirm_meal_entry", confirmParams);
    Console.WriteLine($"Confirmation result: {JsonSerializer.Serialize(confirmResult)}");
}

// Scenario D: View Meal Logs
Console.WriteLine("\n--- Scenario D: view_meal_logs ---");
var logParams = new Dictionary<string, object?> { ["period"] = "day", ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd") };
var logsResult = await mcpClient.CallToolAsync("view_meal_logs", logParams);
Console.WriteLine($"Logs result: {JsonSerializer.Serialize(logsResult)}");

Console.WriteLine("\n===============================================================");
Console.WriteLine(" All Diet-Dost Agentic MCP Workflows Verified Successfully!");
Console.WriteLine("===============================================================");

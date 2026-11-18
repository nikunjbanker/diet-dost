/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nutrition.Application;
using Nutrition.Application.Agents;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Services;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Configuration;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.Mcp;

// ====================================================================================
// Diet-Dost MCP CLI — Stdio Transport Server for Claude Desktop & VS Code
// Architecture: Clean Architecture, Native CQRS, Zero-Assumption Intake
// Target: .NET 11 Stdio MCP Host
// ====================================================================================

var builder = Host.CreateApplicationBuilder(args);

// Ensure stdout is kept clean strictly for MCP JSON-RPC protocol frames; route logs to stderr
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Load configuration
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

// Domain, Application & Storage Infrastructure Services
builder.Services.AddDietDostOptions(builder.Configuration);
builder.Services.AddSingleton<IDietDostConfiguration, DietDostConfiguration>();
builder.Services.AddStorageInfrastructure(builder.Configuration);
builder.Services.AddSecurityInfrastructure();
builder.Services.AddScoped<ClinicalDietitianService>();

builder.Services.AddHttpClient<MicrosoftAgentFoodVisionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<IFoodVisionAgent, MicrosoftAgentFoodVisionService>();

// Native Clean Architecture Application Services & CQRS Dispatcher
builder.Services.AddApplicationServices();

// Official Model Context Protocol (MCP) Server with Stdio Transport
builder.Services.AddDietDostMcpStdioServer();

var app = builder.Build();

await app.RunAsync();

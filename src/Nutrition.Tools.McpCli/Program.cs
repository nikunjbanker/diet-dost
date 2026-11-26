/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
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

// Ensure local SQLite database and schema exist for CLI execution when EF Core is active
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetService<DietTrackerDbContext>();
    if (db != null)
    {
        await db.Database.EnsureCreatedAsync();

        if (db.Database.IsSqlite())
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Ledgers ADD COLUMN HealthScoreStatusText TEXT NOT NULL DEFAULT 'Awaiting First Meal 🌅';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Ledgers ADD COLUMN TargetSugarGrams REAL NOT NULL DEFAULT 25.0;"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Ledgers ADD COLUMN ConsumedSugarGrams REAL NOT NULL DEFAULT 0.0;"); } catch { }
        }
    }
}

await app.RunAsync();

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.AppHost.Extensions;

// .NET Aspire Distributed Application Host (.NET 11 / Aspire 17 Project Resource v2)
// Coordinates distributed components, environment forwarding, local developer telemetry,
// dual-database persistence (SQLite & Azure SQL), and Azure Container Apps infrastructure provisioning.
var builder = DistributedApplication.CreateBuilder(args);

// 1. Declare Azure Container Apps Environment deployment target
var acaEnv = builder.AddAzureContainerAppEnvironment("cae-dietdost");

// 2. Declare Azure Storage for persistent SQLite SMB file share
var storage = builder.AddAzureStorage("dietdost-storage");

// 3. Declare Azure Key Vault for application secrets in deployed environments
var keyVault = builder.AddAzureKeyVault("dietdost-kv");

// 4. Register Web Gateway (Linear-style PWA, API endpoints & AI Vision subsystem)
// Supports dual-database persistence: SQLite (default local / SMB share) or Azure SQL (Serverless / Basic DTU).
// When Azure SQL resource orchestration is enabled in Aspire:
//   var sql = builder.AddAzureSqlServer("dietdost-sql").AddDatabase("dietdost");
//   builder.AddWebGateway(keyVault, database: sql);
builder.AddWebGateway(keyVault);

builder.Build().Run();
